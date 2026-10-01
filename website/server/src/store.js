import { DatabaseSync } from "node:sqlite";

// The only personal data kept is the buyer's e-mail address (to send the key and handle support or a refund).
// `eraseEmail` answers a deletion request without losing the licence itself.
export class Store {
  constructor(path = ":memory:") {
    this.db = new DatabaseSync(path);
    this.db.exec(`
      PRAGMA journal_mode = WAL;
      CREATE TABLE IF NOT EXISTS events (
        id TEXT PRIMARY KEY,
        type TEXT NOT NULL,
        received_at TEXT NOT NULL
      );
      CREATE TABLE IF NOT EXISTS licenses (
        id TEXT PRIMARY KEY,
        session_id TEXT NOT NULL UNIQUE,
        payment_intent TEXT,
        email TEXT,
        plan TEXT NOT NULL,
        status TEXT NOT NULL CHECK (status IN ('active', 'revoked')),
        revoked_reason TEXT,
        created_at TEXT NOT NULL,
        revoked_at TEXT
      );
      CREATE INDEX IF NOT EXISTS licenses_payment_intent ON licenses (payment_intent);
      CREATE INDEX IF NOT EXISTS licenses_email ON licenses (email);
    `);
  }

  // Runs `work` atomically: a failure leaves no half-processed payment, so Stripe's retry can redo it.
  transaction(work) {
    this.db.exec("BEGIN");
    try {
      const result = work();
      this.db.exec("COMMIT");
      return result;
    } catch (error) {
      this.db.exec("ROLLBACK");
      throw error;
    }
  }

  // Returns true the first time an event id is seen, false for a replay.
  recordEvent(id, type, receivedAt) {
    const result = this.db
      .prepare("INSERT OR IGNORE INTO events (id, type, received_at) VALUES (?, ?, ?)")
      .run(id, type, receivedAt);
    return result.changes > 0;
  }

  // One licence per checkout session, whatever the number of times the payment is reported.
  createLicense({ id, sessionId, paymentIntent, email, plan, createdAt }) {
    this.db
      .prepare(
        `INSERT INTO licenses (id, session_id, payment_intent, email, plan, status, created_at)
         VALUES (?, ?, ?, ?, ?, 'active', ?)
         ON CONFLICT (session_id) DO NOTHING`,
      )
      .run(id, sessionId, paymentIntent ?? null, email ?? null, plan, createdAt);
    return this.findBySession(sessionId);
  }

  findBySession(sessionId) {
    return this.db.prepare("SELECT * FROM licenses WHERE session_id = ?").get(sessionId) ?? null;
  }

  revokeByPaymentIntent(paymentIntent, reason, revokedAt) {
    if (!paymentIntent) return 0;
    return this.db
      .prepare(
        `UPDATE licenses SET status = 'revoked', revoked_reason = ?, revoked_at = ?
         WHERE payment_intent = ? AND status = 'active'`,
      )
      .run(reason, revokedAt, paymentIntent).changes;
  }

  eraseEmail(email) {
    return this.db.prepare("UPDATE licenses SET email = NULL WHERE email = ?").run(email).changes;
  }

  purgeEventsBefore(isoDate) {
    return this.db.prepare("DELETE FROM events WHERE received_at < ?").run(isoDate).changes;
  }

  close() {
    this.db.close();
  }
}
