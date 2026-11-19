# 0057: Building panels use the shared template

**Context.** By M21 there are eight hand-built panels (operation, workshop, granary, yard, market, house, citizen,
thatcher rows inside the operation panel), each with its own layout, buttons and wording. The MVPv2 wireframe spec
(`design/source/UI_Design_V2/MVPv2_UI_Wireframe_Spec.md` section 5) defines one template. Owner direction 19/11/2026.

**Decision.**
- Every building panel is built from the shared panel frame in `Assets/Game/UI/Panels/` (title "Name n", status line,
  Pin, Close, drag, tabs, headed sections, construction view). Panels supply content sections; they do not build frames.
- Panels read `*Report` read models only and act only through `SimHost.Execute`.
- A new panel that cannot fit the template needs its own decision record first (design rule `ui.md` UIS-01).

**Consequences.**
- M22b converts every existing panel, including M20 thatcher, M21 daub pit and the house Upgrade.
- PlayMode tests look up section and label names from the frame, so names follow one scheme: `panel-<section>-<item>`.
- Layout changes happen once, in the frame. A one-off panel layout is a review failure.
