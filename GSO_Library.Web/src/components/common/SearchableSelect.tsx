import { useEffect, useRef, useState } from 'react';
import { Form } from 'react-bootstrap';

export interface SearchableSelectOption {
  value: number;
  label: string;
}

interface SearchableSelectProps {
  options: SearchableSelectOption[];
  value: number | null | undefined;
  onChange: (value: number | null) => void;
  placeholder?: string;
  size?: 'sm' | 'lg';
  required?: boolean;
  disabled?: boolean;
}

export default function SearchableSelect({
  options,
  value,
  onChange,
  placeholder = 'Select...',
  size,
  required,
  disabled,
}: SearchableSelectProps) {
  const [search, setSearch] = useState('');
  const [open, setOpen] = useState(false);
  const [highlightIndex, setHighlightIndex] = useState(0);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLDivElement>(null);

  const selectedOption = options.find((o) => o.value === value);

  const filtered = search
    ? options.filter((o) => o.label.toLowerCase().includes(search.toLowerCase()))
    : options;

  // Reset highlight when filtered list changes
  useEffect(() => {
    setHighlightIndex(0);
  }, [search]);

  // Scroll highlighted item into view
  useEffect(() => {
    if (open && listRef.current) {
      const item = listRef.current.children[highlightIndex] as HTMLElement;
      item?.scrollIntoView({ block: 'nearest' });
    }
  }, [highlightIndex, open]);

  // Close on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
        setSearch('');
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const handleSelect = (val: number | null) => {
    onChange(val);
    setOpen(false);
    setSearch('');
    inputRef.current?.blur();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!open) {
      if (e.key === 'ArrowDown' || e.key === 'Enter') {
        e.preventDefault();
        setOpen(true);
      }
      return;
    }

    const itemCount = filtered.length + (required ? 0 : 1); // +1 for the "none" option

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        setHighlightIndex((i) => Math.min(i + 1, itemCount - 1));
        break;
      case 'ArrowUp':
        e.preventDefault();
        setHighlightIndex((i) => Math.max(i - 1, 0));
        break;
      case 'Enter':
        e.preventDefault();
        if (!required && highlightIndex === 0) {
          handleSelect(null);
        } else {
          const idx = required ? highlightIndex : highlightIndex - 1;
          if (filtered[idx]) handleSelect(filtered[idx].value);
        }
        break;
      case 'Escape':
        e.preventDefault();
        setOpen(false);
        setSearch('');
        break;
    }
  };

  return (
    <div ref={containerRef} style={{ position: 'relative' }}>
      <Form.Control
        ref={inputRef}
        size={size}
        value={open ? search : (selectedOption?.label ?? '')}
        placeholder={placeholder}
        onChange={(e) => {
          setSearch(e.target.value);
          if (!open) setOpen(true);
        }}
        onFocus={() => {
          setOpen(true);
          setSearch('');
        }}
        onKeyDown={handleKeyDown}
        required={required && !value}
        disabled={disabled}
        autoComplete="off"
      />
      {open && (
        <div
          ref={listRef}
          style={{
            position: 'absolute',
            top: '100%',
            left: 0,
            right: 0,
            maxHeight: '200px',
            overflowY: 'auto',
            zIndex: 1050,
            border: '1px solid var(--bs-border-color)',
            borderRadius: '0.375rem',
            backgroundColor: 'var(--bs-body-bg)',
            boxShadow: '0 0.5rem 1rem rgba(0,0,0,.15)',
          }}
        >
          {!required && (
            <div
              className={`px-3 py-2 ${highlightIndex === 0 ? 'bg-primary text-white' : ''}`}
              style={{ cursor: 'pointer' }}
              onMouseEnter={() => setHighlightIndex(0)}
              onMouseDown={(e) => { e.preventDefault(); handleSelect(null); }}
            >
              <em className={highlightIndex === 0 ? 'text-white' : 'text-muted'}>{placeholder}</em>
            </div>
          )}
          {filtered.map((option, i) => {
            const idx = required ? i : i + 1;
            return (
              <div
                key={option.value}
                className={`px-3 py-2 ${idx === highlightIndex ? 'bg-primary text-white' : ''}`}
                style={{ cursor: 'pointer' }}
                onMouseEnter={() => setHighlightIndex(idx)}
                onMouseDown={(e) => { e.preventDefault(); handleSelect(option.value); }}
              >
                {option.label}
              </div>
            );
          })}
          {filtered.length === 0 && (
            <div className="px-3 py-2 text-muted">No matches</div>
          )}
        </div>
      )}
    </div>
  );
}
