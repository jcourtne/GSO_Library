CREATE TABLE IF NOT EXISTS instrument_sort_orders (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    is_default BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS instrument_sort_order_items (
    sort_order_id INTEGER NOT NULL REFERENCES instrument_sort_orders(id) ON DELETE CASCADE,
    instrument_id INTEGER NOT NULL REFERENCES instruments(id) ON DELETE CASCADE,
    position INTEGER NOT NULL,
    PRIMARY KEY (sort_order_id, instrument_id)
);
