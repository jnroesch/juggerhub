using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Services.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Sending and receiving files in a conversation (feature 049 / #282).
/// </summary>
[Collection("Chat")]
public sealed class ChatAttachmentTests : ChatTestSupport
{
    public ChatAttachmentTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- US1: a file reaches the other side ------------------------------------

    [Fact]
    public async Task A_document_arrives_for_both_sides_and_downloads_byte_for_byte()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var pdf = Pdf("hall booking confirmation");
        await SendWithFilesAsync(ada, conversationId, "here it is", (pdf, "booking.pdf"));

        foreach (var viewer in new[] { ada, ben })
        {
            var message = (await MessagesAsync(viewer, conversationId)).Single();
            var attachment = message.GetProperty("attachments").EnumerateArray().Single();

            Assert.Equal("booking.pdf", attachment.GetProperty("fileName").GetString());
            Assert.Equal("application/pdf", attachment.GetProperty("contentType").GetString());

            var download = await viewer.GetAsync($"/api/v1/chat/attachments/{attachment.GetProperty("id").GetGuid()}");
            download.EnsureSuccessStatusCode();
            Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
        }
    }

    /// <summary>
    /// FR-004. Before this feature <c>SendAsync</c> refused an empty body outright, so this is the
    /// assertion that the check became "no text AND no files" rather than staying "no text".
    /// </summary>
    [Fact]
    public async Task A_message_can_be_files_alone_with_no_text()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendWithFilesAsync(ada, conversationId, null, (Pdf("rules"), "rules.pdf"));

        var message = (await MessagesAsync(ada, conversationId)).Single();
        Assert.Equal(string.Empty, message.GetProperty("body").GetString());
        Assert.False(message.GetProperty("isUnavailable").GetBoolean());
        Assert.Single(message.GetProperty("attachments").EnumerateArray());
    }

    /// <summary>
    /// The row for a text-free message holds a ZERO-LENGTH body — never <c>Protect("")</c>, which
    /// would be 29 bytes indistinguishable from a short message (feature 047, data-model D2).
    /// </summary>
    [Fact]
    public async Task A_text_free_message_stores_a_zero_length_body()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var messageId = await SendWithFilesAsync(ada, conversationId, null, (Pdf("x"), "x.pdf"));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.ChatMessages.AsNoTracking().FirstAsync(m => m.Id == messageId);

        Assert.Empty(row.BodyCipher);
    }

    [Fact]
    public async Task Text_and_files_arrive_as_one_message()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendWithFilesAsync(ada, conversationId, "schedule attached", (Pdf("s"), "s.pdf"));

        var messages = await MessagesAsync(ada, conversationId);
        Assert.Single(messages);
        Assert.Equal("schedule attached", messages[0].GetProperty("body").GetString());
        Assert.Single(messages[0].GetProperty("attachments").EnumerateArray());
    }

    [Fact]
    public async Task Several_files_keep_the_order_they_were_chosen_in()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendWithFilesAsync(
            ada,
            conversationId,
            null,
            (Pdf("one"), "one.pdf"),
            (Pdf("two"), "two.pdf"),
            (Pdf("three"), "three.pdf"));

        var names = (await MessagesAsync(ada, conversationId))[0]
            .GetProperty("attachments").EnumerateArray()
            .Select(a => a.GetProperty("fileName").GetString())
            .ToList();

        Assert.Equal(["one.pdf", "two.pdf", "three.pdf"], names);
    }

    /// <summary>
    /// FR-024/FR-025: membership is resolved before any byte is fetched, and a refusal is a 404 so
    /// the endpoint never reveals which attachment ids exist.
    /// </summary>
    [Fact]
    public async Task A_non_member_gets_404_rather_than_403()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (carol, _, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendWithFilesAsync(ada, conversationId, null, (Pdf("private"), "private.pdf"));
        var attachmentId = (await MessagesAsync(ada, conversationId))[0]
            .GetProperty("attachments").EnumerateArray().Single()
            .GetProperty("id").GetGuid();

        var real = await carol.GetAsync($"/api/v1/chat/attachments/{attachmentId}");
        var invented = await carol.GetAsync($"/api/v1/chat/attachments/{Guid.CreateVersion7()}");

        // Identical answers: a stranger cannot tell a real id from one that never existed.
        Assert.Equal(HttpStatusCode.NotFound, real.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invented.StatusCode);
    }

    // --- US2: images -----------------------------------------------------------

    /// <summary>
    /// SC-003, asserted against the STORED bytes rather than against the code path: a shared photo
    /// must carry no location, camera or colour-profile data, and must already be upright.
    /// </summary>
    [Fact]
    public async Task A_photo_is_stripped_of_metadata_and_stored_upright_as_webp()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        // 2 wide, 1 tall, flagged "rotate 90° CW" — so an honoured orientation makes it 1x2.
        using var image = new Image<Rgba32>(2, 1, new Rgba32(255, 255, 255));
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, (ushort)6);
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        image.Metadata.ExifProfile = exif;

        await SendWithFilesAsync(ada, conversationId, null, (Encode(image, new JpegEncoder()), "holiday.jpg"));

        var attachment = (await MessagesAsync(ada, conversationId))[0]
            .GetProperty("attachments").EnumerateArray().Single();

        Assert.Equal("image/webp", attachment.GetProperty("contentType").GetString());
        Assert.Equal(1, attachment.GetProperty("width").GetInt32());
        Assert.Equal(2, attachment.GetProperty("height").GetInt32());

        var download = await ada.GetAsync($"/api/v1/chat/attachments/{attachment.GetProperty("id").GetGuid()}");
        download.EnsureSuccessStatusCode();

        using var stored = Image.Load(await download.Content.ReadAsByteArrayAsync());
        Assert.Null(stored.Metadata.ExifProfile);
        Assert.Null(stored.Metadata.IptcProfile);
        Assert.Null(stored.Metadata.XmpProfile);
    }

    /// <summary>
    /// FR-026 + R1: an image renders inline, everything else is a download. The application has no
    /// CSP header, so this disposition is the control that stops a member-supplied file being
    /// rendered in our own origin.
    /// </summary>
    [Fact]
    public async Task A_document_downloads_and_an_image_does_not()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        using var image = new Image<Rgba32>(8, 8, new Rgba32(10, 120, 200));
        await SendWithFilesAsync(
            ada,
            conversationId,
            null,
            (Encode(image, new PngEncoder()), "pitch.png"),
            (Pdf("rules"), "rules.pdf"));

        var attachments = (await MessagesAsync(ada, conversationId))[0]
            .GetProperty("attachments").EnumerateArray().ToList();

        var png = attachments.Single(a => a.GetProperty("contentType").GetString() == "image/webp");
        var pdf = attachments.Single(a => a.GetProperty("contentType").GetString() == "application/pdf");

        var imageResponse = await ada.GetAsync($"/api/v1/chat/attachments/{png.GetProperty("id").GetGuid()}");
        var documentResponse = await ada.GetAsync($"/api/v1/chat/attachments/{pdf.GetProperty("id").GetGuid()}");

        Assert.Null(imageResponse.Content.Headers.ContentDisposition);
        Assert.Equal("attachment", documentResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("rules.pdf", documentResponse.Content.Headers.ContentDisposition?.FileNameStar);
    }

    // --- US3: refusals ---------------------------------------------------------

    [Fact]
    public async Task An_executable_renamed_as_an_image_is_refused()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        // An ELF header — not an image, whatever the name says.
        var content = new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00, 0x00, 0x00 };
        var resp = await PostFilesAsync(ada, conversationId, null, (content, "totally.jpg"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        await AssertNothingWasStoredAsync(ada, conversationId);
    }

    /// <summary>
    /// The OOXML trap: every .docx IS a ZIP, so a signature check alone accepts any archive wearing
    /// the extension — which is how a bundle of executables gets past an allow-list that believes
    /// it is strict.
    /// </summary>
    [Fact]
    public async Task A_plain_zip_renamed_as_a_document_is_refused()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var resp = await PostFilesAsync(ada, conversationId, null, (ZipContaining("payload.exe"), "report.docx"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        await AssertNothingWasStoredAsync(ada, conversationId);
    }

    [Fact]
    public async Task A_real_word_document_is_accepted()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendWithFilesAsync(ada, conversationId, null, (WordDocument(), "notes.docx"));

        var attachment = (await MessagesAsync(ada, conversationId))[0]
            .GetProperty("attachments").EnumerateArray().Single();

        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            attachment.GetProperty("contentType").GetString());
    }

    [Fact]
    public async Task A_truncated_image_is_refused_as_unreadable_not_stored()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        using var image = new Image<Rgba32>(64, 64, new Rgba32(1, 2, 3));
        var png = Encode(image, new PngEncoder());
        var truncated = png[..(png.Length / 2)];

        var resp = await PostFilesAsync(ada, conversationId, null, (truncated, "broken.png"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        await AssertNothingWasStoredAsync(ada, conversationId);
    }

    [Fact]
    public async Task An_eleventh_file_is_refused()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var files = Enumerable.Range(0, ChatConstants.MaxAttachmentsPerMessage + 1)
            .Select(i => (Pdf($"file {i}"), $"f{i}.pdf"))
            .ToArray();

        var resp = await PostFilesAsync(ada, conversationId, null, files);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        await AssertNothingWasStoredAsync(ada, conversationId);
    }

    [Fact]
    public async Task A_send_with_neither_text_nor_files_is_refused()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var resp = await PostFilesAsync(ada, conversationId, null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        await AssertNothingWasStoredAsync(ada, conversationId);
    }

    /// <summary>
    /// SC-006: a refusal in the middle of a set leaves NOTHING behind — not the files that were
    /// fine, not a message, not a stored object.
    /// </summary>
    [Fact]
    public async Task One_bad_file_among_good_ones_stores_none_of_them()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var resp = await PostFilesAsync(
            ada,
            conversationId,
            "a few things",
            (Pdf("fine"), "fine.pdf"),
            (new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, "nope.bin"),
            (Pdf("also fine"), "also-fine.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        await AssertNothingWasStoredAsync(ada, conversationId);
    }

    // --- The extension is the part that does something -------------------------

    /// <summary>
    /// Every operating system opens a downloaded file by its extension, so serving the sender's
    /// name unchanged would leave the thing that decides which application runs attacker-controlled
    /// — and free to disagree with the content we validated. Validating bytes and then handing over
    /// a contradictory name gives away most of what the allow-list is for.
    /// </summary>
    [Theory]
    [InlineData("payload.exe")]
    [InlineData("payload.docm")]
    [InlineData("payload")]
    public async Task The_served_name_takes_its_extension_from_what_the_file_actually_is(string sent)
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        await SendWithFilesAsync(ada, conversationId, null, (WordDocument(), sent));

        var attachment = (await MessagesAsync(ada, conversationId))[0]
            .GetProperty("attachments").EnumerateArray().Single();

        Assert.Equal("payload.docx", attachment.GetProperty("fileName").GetString());

        var download = await ada.GetAsync($"/api/v1/chat/attachments/{attachment.GetProperty("id").GetGuid()}");
        Assert.Equal("payload.docx", download.Content.Headers.ContentDisposition?.FileNameStar);
    }

    /// <summary>An image is re-encoded, so `.jpg` really is a `.webp` afterwards — say so.</summary>
    [Fact]
    public async Task A_normalized_image_is_served_under_its_real_extension()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        using var image = new Image<Rgba32>(8, 8, new Rgba32(10, 120, 200));
        await SendWithFilesAsync(ada, conversationId, null, (Encode(image, new JpegEncoder()), "holiday.jpg"));

        var attachment = (await MessagesAsync(ada, conversationId))[0]
            .GetProperty("attachments").EnumerateArray().Single();

        Assert.Equal("holiday.webp", attachment.GetProperty("fileName").GetString());
    }

    /// <summary>
    /// A macro-enabled Office file declares its main part as the macroEnabled type, which the
    /// allow-list does not contain — so a genuine .docm is refused on its content, not its name.
    /// </summary>
    [Fact]
    public async Task A_macro_enabled_document_is_refused()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var docm = OpenXml(
            "application/vnd.ms-word.document.macroEnabled.main+xml",
            ("word/vbaProject.bin", "MACRO"));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await PostFilesAsync(ada, conversationId, null, (docm, "notes.docm"))).StatusCode);

        await AssertNothingWasStoredAsync(ada, conversationId);
    }

    /// <summary>
    /// The bypass the manifest check alone allows: an archive that declares the macro-FREE override
    /// — satisfying a substring test — while still carrying a macro project. Whether Word would
    /// honour it depends on the extension and the declared part, and "probably not exploitable" is
    /// not a control. A real .docx never contains this part.
    /// </summary>
    [Fact]
    public async Task A_document_carrying_a_macro_project_is_refused_however_it_declares_itself()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var hybrid = OpenXml(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
            ("word/vbaProject.bin", "MACRO"));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await PostFilesAsync(ada, conversationId, null, (hybrid, "invoice.docx"))).StatusCode);

        await AssertNothingWasStoredAsync(ada, conversationId);
    }

    // --- US4: withdrawal -------------------------------------------------------

    /// <summary>
    /// FR-029/FR-030: withdrawing takes the files with it — the rows, the names, and the stored
    /// objects. Otherwise "delete" would quietly mean "unlist".
    /// </summary>
    [Fact]
    public async Task Withdrawing_a_message_makes_its_files_unreachable()
    {
        var (ada, _, _) = await NewUserAsync();
        var (ben, benId, _) = await NewUserAsync();
        var conversationId = await StartDirectAsync(ada, benId);

        var messageId = await SendWithFilesAsync(ada, conversationId, "oops", (Pdf("secret"), "secret.pdf"));
        var attachmentId = (await MessagesAsync(ada, conversationId))[0]
            .GetProperty("attachments").EnumerateArray().Single()
            .GetProperty("id").GetGuid();

        // Ben could read it a moment ago.
        (await ben.GetAsync($"/api/v1/chat/attachments/{attachmentId}")).EnsureSuccessStatusCode();

        (await ada.DeleteAsync($"/api/v1/chat/messages/{messageId}")).EnsureSuccessStatusCode();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await ben.GetAsync($"/api/v1/chat/attachments/{attachmentId}")).StatusCode);

        // The tombstone carries no file names or counts — those are content too.
        var tombstone = (await MessagesAsync(ben, conversationId))[0];
        Assert.True(tombstone.GetProperty("isDeleted").GetBoolean());
        Assert.Empty(tombstone.GetProperty("attachments").EnumerateArray());

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ChatAttachments.AsNoTracking().AnyAsync(a => a.Id == attachmentId));
    }

    // --- Helpers ---------------------------------------------------------------

    private static async Task<Guid> SendWithFilesAsync(
        HttpClient client,
        Guid conversationId,
        string? body,
        params (byte[] Content, string Name)[] files)
    {
        var resp = await PostFilesAsync(client, conversationId, body, files);
        Assert.True(resp.IsSuccessStatusCode, $"send failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        var created = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> PostFilesAsync(
        HttpClient client,
        Guid conversationId,
        string? body,
        params (byte[] Content, string Name)[] files)
    {
        using var form = new MultipartFormDataContent();
        if (body is not null)
        {
            form.Add(new StringContent(body), "body");
        }

        foreach (var (content, name) in files)
        {
            var part = new ByteArrayContent(content);
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(part, "files", name);
        }

        return await client.PostAsync($"/api/v1/chat/conversations/{conversationId}/messages", form);
    }

    private static async Task<List<JsonElement>> MessagesAsync(HttpClient client, Guid conversationId)
    {
        var page = await GetMessagesAsync(client, conversationId);
        return [.. page.GetProperty("items").EnumerateArray()];
    }

    /// <summary>A refused send must leave the conversation exactly as it was.</summary>
    private async Task AssertNothingWasStoredAsync(HttpClient client, Guid conversationId)
    {
        Assert.Empty(await MessagesAsync(client, conversationId));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ChatAttachments.AsNoTracking()
            .AnyAsync(a => a.Message.ConversationId == conversationId));
    }

    private static byte[] Pdf(string text) =>
        Encoding.ASCII.GetBytes($"%PDF-1.4\n% test fixture\n{text}\n%%EOF\n");

    /// <summary>A ZIP that is not an OOXML document — no <c>[Content_Types].xml</c>.</summary>
    private static byte[] ZipContaining(string entryName)
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            using var entry = archive.CreateEntry(entryName).Open();
            entry.Write("not a document"u8);
        }

        return ms.ToArray();
    }

    /// <summary>An OOXML archive declaring <paramref name="mainPartType"/>, plus any extra parts.</summary>
    private static byte[] OpenXml(string mainPartType, params (string Name, string Content)[] extras)
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            using (var entry = archive.CreateEntry("[Content_Types].xml").Open())
            {
                entry.Write(System.Text.Encoding.UTF8.GetBytes(
                    $"""<?xml version="1.0"?><Types><Override PartName="/word/document.xml" ContentType="{mainPartType}"/></Types>"""));
            }

            foreach (var (name, content) in extras)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(System.Text.Encoding.UTF8.GetBytes(content));
            }
        }

        return ms.ToArray();
    }

    /// <summary>The minimum that makes a ZIP a Word document: the content-types part.</summary>
    private static byte[] WordDocument()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            using var entry = archive.CreateEntry("[Content_Types].xml").Open();
            entry.Write(
                """<?xml version="1.0"?><Types><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>"""u8);
        }

        return ms.ToArray();
    }

    private static byte[] Encode(Image image, SixLabors.ImageSharp.Formats.IImageEncoder encoder)
    {
        using var ms = new MemoryStream();
        image.Save(ms, encoder);
        return ms.ToArray();
    }
}
