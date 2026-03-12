import { FileSizePipe } from './file-size.pipe';

describe('FileSizePipe', () => {
  const pipe = new FileSizePipe();

  it('should return "0 B" for 0 bytes', () => {
    expect(pipe.transform(0)).toBe('0 B');
  });

  it('should format bytes correctly', () => {
    expect(pipe.transform(500)).toBe('500 B');
  });

  it('should format kilobytes correctly', () => {
    expect(pipe.transform(1024)).toBe('1.0 KB');
    expect(pipe.transform(2560)).toBe('2.5 KB');
  });

  it('should format megabytes correctly', () => {
    expect(pipe.transform(1048576)).toBe('1.0 MB');
    expect(pipe.transform(5242880)).toBe('5.0 MB');
  });

  it('should format gigabytes correctly', () => {
    expect(pipe.transform(1073741824)).toBe('1.0 GB');
    expect(pipe.transform(2147483648)).toBe('2.0 GB');
  });

  it('should handle small file sizes', () => {
    expect(pipe.transform(1)).toBe('1 B');
    expect(pipe.transform(100)).toBe('100 B');
  });

  it('should handle fractional KB', () => {
    const result = pipe.transform(1536);
    expect(result).toBe('1.5 KB');
  });
});
