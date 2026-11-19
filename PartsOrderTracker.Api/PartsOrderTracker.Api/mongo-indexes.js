// Run manually in mongosh after connecting to the intended Atlas cluster.
// This script creates indexes only; it never modifies or deletes documents.
db = db.getSiblingDB("Parts_order_tracker_dev");

db.parts.createIndex(
  { articleNumber: 1 },
  { unique: true, name: "ux_parts_articleNumber" }
);

db.orders.createIndex(
  { userId: 1, createdAt: -1 },
  { name: "ix_orders_userId_createdAt" }
);

db.orders.createIndex(
  { status: 1, nextStatusChangeAt: 1 },
  { name: "ix_orders_status_nextStatusChangeAt" }
);
