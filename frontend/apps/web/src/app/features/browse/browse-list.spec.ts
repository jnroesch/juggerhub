import { Observable, of, throwError } from 'rxjs';
import { PagedResult } from '../../core/models/search.models';
import { BrowseList } from './browse-list';

interface Row {
  id: number;
}

function page(items: Row[], total: number, skip = 0, take = 20): PagedResult<Row> {
  return { items, totalCount: total, skip, take };
}

describe('BrowseList', () => {
  it('starts in the loading state before any load resolves', () => {
    const list = new BrowseList<Row>(() => new Observable<PagedResult<Row>>());
    expect(list.state()).toBe('loading');
    expect(list.items()).toEqual([]);
  });

  it('moves to ready and exposes items + total after a successful reload', () => {
    const list = new BrowseList<Row>(() => of(page([{ id: 1 }, { id: 2 }], 5)));
    list.reload();
    expect(list.state()).toBe('ready');
    expect(list.items()).toHaveLength(2);
    expect(list.total()).toBe(5);
    expect(list.hasMore()).toBe(true);
  });

  it('shows the empty state when there is no data and no active filter', () => {
    const list = new BrowseList<Row>(() => of(page([], 0)));
    list.filtered.set(false);
    list.reload();
    expect(list.state()).toBe('empty');
  });

  it('shows the no-results state when a query/filter matched nothing', () => {
    const list = new BrowseList<Row>(() => of(page([], 0)));
    list.filtered.set(true);
    list.reload();
    expect(list.state()).toBe('no-results');
  });

  it('shows the error state when the fetch fails', () => {
    const list = new BrowseList<Row>(() => throwError(() => new Error('boom')));
    list.reload();
    expect(list.state()).toBe('error');
  });

  it('appends the next page on loadMore and stops at the end', () => {
    let call = 0;
    const list = new BrowseList<Row>((skip) => {
      call += 1;
      return skip === 0 ? of(page([{ id: 1 }, { id: 2 }], 3, 0)) : of(page([{ id: 3 }], 3, 2));
    });

    list.reload();
    expect(list.items()).toHaveLength(2);
    expect(list.hasMore()).toBe(true);

    list.loadMore();
    expect(list.items().map((r) => r.id)).toEqual([1, 2, 3]);
    expect(list.hasMore()).toBe(false);

    // No further fetch once the end is reached.
    list.loadMore();
    expect(call).toBe(2);
  });

  it('reload replaces items rather than appending (first-page reset)', () => {
    let batch = [{ id: 1 }];
    const list = new BrowseList<Row>(() => of(page(batch, batch.length)));
    list.reload();
    expect(list.items().map((r) => r.id)).toEqual([1]);

    batch = [{ id: 9 }];
    list.reload();
    expect(list.items().map((r) => r.id)).toEqual([9]);
  });
});
