import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PageContainerComponent, PageWidth } from './page-container.component';

@Component({
  imports: [PageContainerComponent],
  template: `<jh-page-container [width]="width()"><p data-testid="c">Page</p></jh-page-container>`,
})
class HostComponent {
  readonly width = signal<PageWidth>('md');
}

describe('PageContainerComponent (jh-page-container)', () => {
  let fixture: ComponentFixture<HostComponent>;

  function inner(): HTMLElement {
    return fixture.nativeElement.querySelector('jh-page-container > div') as HTMLElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('centers content and defaults to the md container width', () => {
    expect(inner().classList).toContain('mx-auto');
    expect(inner().classList).toContain('max-w-container-md');
  });

  it('projects content', () => {
    expect(inner().querySelector('[data-testid="c"]')?.textContent).toBe('Page');
  });

  it('maps the width input to the container token, without leaking the previous width', () => {
    fixture.componentInstance.width.set('lg');
    fixture.detectChanges();
    expect(inner().classList).toContain('max-w-container-lg');
    expect(inner().classList).not.toContain('max-w-container-md');
  });
});
