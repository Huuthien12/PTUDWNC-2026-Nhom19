import assert from "node:assert/strict";
import test from "node:test";
import { useState } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import AuthNotice from "../src/components/auth/AuthNotice";

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });

function NoticeHarness({ kind }: { kind: "error" | "success" }) {
  const [message, setMessage] = useState("Thông báo đầu tiên");
  return <>
    {message && <AuthNotice kind={kind} onDismiss={() => setMessage("")}>{message}</AuthNotice>}
    <button type="button" onClick={() => setMessage("Thông báo mới")}>Hiện thông báo mới</button>
  </>;
}

for (const kind of ["error", "success"] as const) {
  test(`${kind} notice can be dismissed and a new notice is announced`, async () => {
    let view!: ReactTestRenderer;
    await act(async () => { view = create(<NoticeHarness kind={kind} />); });

    const notice = view.root.findByProps({ role: kind === "error" ? "alert" : "status" });
    assert.equal(notice.props["aria-live"], kind === "error" ? "assertive" : "polite");
    const dismiss = view.root.findByProps({
      "aria-label": kind === "error" ? "Đóng thông báo lỗi" : "Đóng thông báo thành công",
    });

    await act(async () => { dismiss.props.onClick(); });
    assert.equal(view.root.findAllByProps({ role: kind === "error" ? "alert" : "status" }).length, 0);

    await act(async () => { view.root.findByProps({ children: "Hiện thông báo mới" }).props.onClick(); });
    assert.match(JSON.stringify(view.toJSON()), /Thông báo mới/);
  });
}
