import { Pagination as BsPagination, Form } from 'react-bootstrap';

interface PaginationProps {
  page: number;
  totalPages: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}

export default function Pagination({ page, totalPages, pageSize, onPageChange, onPageSizeChange }: PaginationProps) {
  const pages: number[] = [];
  const start = Math.max(1, page - 2);
  const end = Math.min(totalPages, page + 2);
  for (let i = start; i <= end; i++) pages.push(i);

  return (
    <div className="d-flex justify-content-between align-items-center flex-wrap gap-2">
      <BsPagination className="mb-0">
        <BsPagination.First onClick={() => onPageChange(1)} disabled={page === 1} />
        <BsPagination.Prev onClick={() => onPageChange(page - 1)} disabled={page === 1} />
        {start > 1 && <BsPagination.Ellipsis disabled />}
        {pages.map((p) => (
          <BsPagination.Item key={p} active={p === page} onClick={() => onPageChange(p)}>
            {p}
          </BsPagination.Item>
        ))}
        {end < totalPages && <BsPagination.Ellipsis disabled />}
        <BsPagination.Next onClick={() => onPageChange(page + 1)} disabled={page === totalPages} />
        <BsPagination.Last onClick={() => onPageChange(totalPages)} disabled={page === totalPages} />
      </BsPagination>
      <Form.Select
        size="sm"
        style={{ width: 'auto' }}
        value={pageSize}
        onChange={(e) => onPageSizeChange(Number(e.target.value))}
      >
        {[10, 20, 50, 100].map((s) => (
          <option key={s} value={s}>{s} per page</option>
        ))}
      </Form.Select>
    </div>
  );
}
