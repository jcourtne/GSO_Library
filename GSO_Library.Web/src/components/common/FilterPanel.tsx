import { useEffect, useState } from 'react';
import { ButtonGroup, Form } from 'react-bootstrap';

interface FilterPanelSectionProps {
  label: string;
  options: { value: string | number; label: string }[];
  selected: (string | number)[];
  onChange: (selected: (string | number)[]) => void;
  matchMode?: 'any' | 'all';
  onMatchModeChange?: (mode: 'any' | 'all') => void;
}

export default function FilterPanelSection({ label, options, selected, onChange, matchMode, onMatchModeChange }: FilterPanelSectionProps) {
  const [search, setSearch] = useState('');

  useEffect(() => {
    if (selected.length === 0) setSearch('');
  }, [selected.length]);

  const sorted = [
    ...options.filter((o) => selected.includes(o.value)),
    ...options.filter((o) => !selected.includes(o.value)),
  ];
  const visible = search
    ? sorted.filter((o) => o.label.toLowerCase().includes(search.toLowerCase())).slice(0, 8)
    : sorted.slice(0, 8);

  const toggle = (value: string | number) => {
    if (selected.includes(value)) {
      onChange(selected.filter((v) => v !== value));
    } else {
      onChange([...selected, value]);
    }
  };

  if (options.length === 0) return null;

  return (
    <div className="mb-3">
      <div className="d-flex justify-content-between align-items-center mb-1">
        <strong className="small text-uppercase text-muted" style={{ fontSize: '0.7rem', letterSpacing: '0.05em' }}>
          {label}
          {selected.length > 0 && (
            <span className="badge bg-primary ms-1" style={{ fontSize: '0.65rem' }}>{selected.length}</span>
          )}
        </strong>
        <div className="d-flex align-items-center gap-2">
          {onMatchModeChange && selected.length >= 2 && (
            <ButtonGroup size="sm">
              <button
                type="button"
                className={`btn btn-outline-primary py-0${matchMode === 'any' ? ' active' : ''}`}
                style={{ fontSize: '0.65rem' }}
                onClick={() => onMatchModeChange('any')}
              >
                Any
              </button>
              <button
                type="button"
                className={`btn btn-outline-primary py-0${matchMode === 'all' ? ' active' : ''}`}
                style={{ fontSize: '0.65rem' }}
                onClick={() => onMatchModeChange('all')}
              >
                All
              </button>
            </ButtonGroup>
          )}
          {selected.length > 0 && (
            <button
              type="button"
              className="btn btn-link btn-sm p-0 text-decoration-none"
              style={{ fontSize: '0.75rem' }}
              onClick={() => { onChange([]); setSearch(''); }}
            >
              Clear
            </button>
          )}
        </div>
      </div>
      {options.length > 8 && (
        <Form.Control
          size="sm"
          placeholder={`Search…`}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="mb-1"
        />
      )}
      {visible.map((opt) => (
        <Form.Check
          key={opt.value}
          type="checkbox"
          id={`filter-${label}-${opt.value}`}
          label={opt.label}
          checked={selected.includes(opt.value)}
          onChange={() => toggle(opt.value)}
          className="small"
        />
      ))}
    </div>
  );
}
