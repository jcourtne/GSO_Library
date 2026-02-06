import { Table, Spinner } from 'react-bootstrap';

interface Column<T> {
  key: string;
  label: string;
  sortable?: boolean;
  render?: (item: T) => React.ReactNode;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  isLoading?: boolean;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  onSort?: (key: string) => void;
  onRowClick?: (item: T) => void;
}

export default function DataTable<T extends { id: number | string }>({
  columns,
  data,
  isLoading,
  sortBy,
  sortDirection,
  onSort,
  onRowClick,
}: DataTableProps<T>) {
  const getSortIcon = (key: string) => {
    if (sortBy !== key) return ' \u2195';
    return sortDirection === 'asc' ? ' \u2191' : ' \u2193';
  };

  if (isLoading) {
    return (
      <div className="text-center py-5">
        <Spinner animation="border" role="status">
          <span className="visually-hidden">Loading...</span>
        </Spinner>
      </div>
    );
  }

  if (data.length === 0) {
    return <p className="text-muted text-center py-4">No records found.</p>;
  }

  return (
    <Table striped hover responsive>
      <thead>
        <tr>
          {columns.map((col) => (
            <th
              key={col.key}
              style={col.sortable ? { cursor: 'pointer', userSelect: 'none' } : undefined}
              onClick={col.sortable && onSort ? () => onSort(col.key) : undefined}
            >
              {col.label}
              {col.sortable && getSortIcon(col.key)}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {data.map((item) => (
          <tr
            key={item.id}
            onClick={onRowClick ? () => onRowClick(item) : undefined}
            style={onRowClick ? { cursor: 'pointer' } : undefined}
          >
            {columns.map((col) => (
              <td key={col.key}>
                {col.render
                  ? col.render(item)
                  : String((item as Record<string, unknown>)[col.key] ?? '')}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
