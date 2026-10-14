import assert from "node:assert/strict";
import test from "node:test";
import { profileSchema } from "../src/services/profile-schema";

test("profile form accepts backend bounds and trims editable values", () => {
  assert.deepEqual(profileSchema.parse({ fullName: "  An  ", avatarUrl: "" }),
    { fullName: "An", avatarUrl: "" });
  assert.equal(profileSchema.safeParse({ fullName: "A", avatarUrl: "" }).success, false);
  assert.equal(profileSchema.safeParse({ fullName: "A".repeat(101), avatarUrl: "" }).success, false);
  assert.equal(profileSchema.safeParse({ fullName: "Valid Name", avatarUrl: "not a URL" }).success, false);
  assert.equal(profileSchema.safeParse({ fullName: "Valid Name", avatarUrl: "https://example.test/a.png" }).success, true);
});
