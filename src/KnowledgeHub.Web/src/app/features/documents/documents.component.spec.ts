import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { DocumentsComponent } from './documents.component';
import { DocumentService, Document } from '../../core/services/document.service';
import { environment } from '../../../environments/environment';
import { of } from 'rxjs';

describe('DocumentsComponent', () => {
  let component: DocumentsComponent;
  let fixture: ComponentFixture<DocumentsComponent>;
  let docService: DocumentService;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentsComponent, TranslateModule.forRoot()],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate: vi.fn() } },
      ],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);

    // Discard /v1/auth/me request from AuthService constructor
    const meRequests = httpTesting.match(`${environment.apiUrl}/v1/auth/me`);
    meRequests.forEach((req) => req.flush(null, { status: 401, statusText: 'Unauthorized' }));

    fixture = TestBed.createComponent(DocumentsComponent);
    component = fixture.componentInstance;
    docService = TestBed.inject(DocumentService);
  });

  afterEach(() => {
    // Flush any remaining document requests to prevent cascade failures
    const remainingDocs = httpTesting.match(`${environment.apiUrl}/v1/documents`);
    remainingDocs.forEach((req) => req.flush({ items: [] }));

    httpTesting.verify();
    localStorage.clear();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should call loadDocuments on init', () => {
    const loadSpy = vi.spyOn(docService, 'loadDocuments');

    fixture.detectChanges();

    expect(loadSpy).toHaveBeenCalled();

    // Flush the HTTP request from loadDocuments
    const docReqs = httpTesting.match(`${environment.apiUrl}/v1/documents`);
    docReqs.forEach((req) => req.flush({ items: [] }));
  });

  it('should render document list when documents exist', () => {
    const mockDocs: Document[] = [
      {
        id: '1',
        fileName: 'test.pdf',
        contentType: 'application/pdf',
        fileSize: 1024,
        status: 'Ready',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
      {
        id: '2',
        fileName: 'doc.txt',
        contentType: 'text/plain',
        fileSize: 512,
        status: 'Processing',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
    ];

    // Trigger ngOnInit and flush loadDocuments with mock docs
    fixture.detectChanges();
    const docReqs = httpTesting.match(`${environment.apiUrl}/v1/documents`);
    docReqs.forEach((req) => req.flush({ items: mockDocs }));

    // Re-render after async data arrived
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
  });

  it('should show empty state when no documents', () => {
    // Trigger ngOnInit and flush loadDocuments with empty list
    fixture.detectChanges();
    const docReqs = httpTesting.match(`${environment.apiUrl}/v1/documents`);
    docReqs.forEach((req) => req.flush({ items: [] }));

    // Re-render after async data arrived
    fixture.detectChanges();

    const emptyText = fixture.nativeElement.textContent;
    expect(emptyText).toContain('No documents yet');
  });

  it('should trigger file input on upload zone click', () => {
    fixture.detectChanges();

    // Flush any pending requests
    const docReqs = httpTesting.match(`${environment.apiUrl}/v1/documents`);
    docReqs.forEach((req) => req.flush({ items: [] }));

    const uploadZone = fixture.nativeElement.querySelector('.border-dashed');
    expect(uploadZone).toBeTruthy();
  });

  it('should call delete on document service', () => {
    const deleteSpy = vi.spyOn(docService, 'delete').mockReturnValue(of(undefined));

    const doc: Document = {
      id: '123',
      fileName: 'delete-me.pdf',
      contentType: 'application/pdf',
      fileSize: 100,
      status: 'Ready',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    component.deleteDoc(doc);

    expect(deleteSpy).toHaveBeenCalledWith('123');
  });

  it('should set isUploading during file upload', () => {
    expect(component.isUploading).toBe(false);
  });
});
