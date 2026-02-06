# KanbanFiles Specifications

## Phase Dependencies

Each phase builds on the previous ones:

1. **Phase 1: Scaffolding & Core Models** ✅ COMPLETE
   - Foundation for all other phases
   - Required by: All phases

2. **Phase 2: Basic Board UI** ✅ COMPLETE
   - Requires: Phase 1
   - Required by: Phase 3, 4, 5, 6, 7

3. **Phase 3: Drag & Drop** ✅ COMPLETE
   - Requires: Phase 2
   - Required by: Phase 5 (grouping with drag/drop)

4. **Phase 4: Two-Way File Sync** ✅ COMPLETE
   - Requires: Phase 2
   - Can be done in parallel with Phase 3

5. **Phase 5: Grouping** ✅ COMPLETE
   - Requires: Phase 2, Phase 3
   - Required by: None (feature extension)

6. **Phase 6: Rich Markdown Editing** 🚫 BLOCKED
   - Requires: Phase 2
   - WindowsAppSDK 1.8 XAML Compiler bug prevents adding new XAML files
   - Can proceed with ContentDialog workaround or SDK upgrade
   - Can be done in parallel with Phases 3, 4, 5

7. **Phase 7: Polish & Edge Cases**
   - Requires: All previous phases (Phase 6 optional)
   - Final refinements and polish

## Implementation Order

**Recommended sequence:**
1. ✅ Phase 1: Scaffolding & Core Models
2. ✅ Phase 2: Basic Board UI
3. ✅ Phase 3: Drag & Drop
4. ✅ Phase 4: Two-Way File Sync
5. ✅ Phase 5: Grouping
6. 🚫 **Phase 6: Rich Markdown Editing** ← BLOCKED (WindowsAppSDK 1.8 XAML Compiler bug)
7. **Phase 7: Polish & Edge Cases** ← CAN PROCEED (Phase 6 optional)

**Completed:** 5/7 phases (71.4%)
**Blocked:** Phase 6 due to tooling bug (see ISSUES.md 2/6/2026 7:56 PM)
**Next:** Phase 7 or investigate Phase 6 workarounds