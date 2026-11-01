# InnoEngine.VisualNovel working rules

- Priority: architecture and ownership; approachable complete workflows; readable code; performance.
- All mutations, generated data, test projects and reports stay inside this repository. The sibling InnoEngine checkout is read-only and may be changing concurrently.
- Use only the formal scripting API in Assets/Code. Tooling and tests may consume public host APIs from an explicitly selected existing engine build. Never add production binary references to bypass script API restrictions.
- Before adding a general-purpose type or service, inspect the formal Core scripting surface and reuse suitable engine contracts. Keep a native Scene -> GameObject -> GameComponent/GameBehavior game entry and lifecycle; VN session state must not become a parallel scene or object framework.
- Do not depend on Rendering2D, Canvas or another feature plugin.
- Assets/Code uses ten planned assembly boundaries. Runtime must not reference Compiler, Authoring or Editor. Add an assembly only when it contains an implemented responsibility.
- One graph is the control-flow authority. Script and graph views share edits, identity, compilation and engine History.
- Reuse engine Serialization, Graph, Events, Identity, Assets and Editor History. No parallel JSON persistence, event bus, asset database or undo stack.
- Public/protected APIs require meaningful English XML documentation. Comments are English; product documentation is Chinese.
- Namespaces follow feature directories. Private fields use m_. Functions with multiple parameters put each parameter on its own line and use `) {`.
- No global using, InternalsVisibleTo, reflection bypass, legacy formats, schema versions or placeholder implementations.
- Run formal script compilation and behavior tests. Never describe unimplemented or unverified plan requirements as complete.
- Maintain Documentation/Specifications/FeatureMatrix.md and Documentation/ImplementationStatus.md with actual evidence and blockers.
