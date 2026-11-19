# Action Jackson handoff review: documented behavior and missing source/runtime files

## Summary

The handoff documents provide a useful map of Action Jackson's design and acknowledge its unresolved behavior. The current GitHub snapshot is incomplete relative to its own file inventory: **the documented `source/` subfolder is absent**, so the application implementation cannot yet be inspected or rebuilt from this published snapshot.

**Reviewed commit:** [`fd9bf36`](https://github.com/EverForgeLabsHQ/EverForge-Community/commit/fd9bf368516b419b9cef1bf591d23c7ff61ec97a) on `main`  
**Review date:** November 19, 2026  
**Scope:** Documentation and repository completeness. No executable was launched, no microphone or mouse-input tests were run, and no live-site compatibility was verified.

This finding concerns the published snapshot. It does not dispute what was present in the author's original local handoff, and it does not diagnose why the original installed application behaves differently on Ignition.

## 1. What the handoff documents describe

The [code guide](https://github.com/EverForgeLabsHQ/EverForge-Community/blob/fd9bf368516b419b9cef1bf591d23c7ff61ec97a/Action%20Jackson%20Source/01%20CODE%20GUIDE.txt) describes:

| Area | Documented implementation |
| --- | --- |
| Application | Windows x64, C#, Windows Forms |
| Speech | Local Vosk recognition; a Windows Speech fallback when the model folder is absent |
| Detection | Windows OCR plus Windows UI Automation; visible-screen and accessible-control filtering |
| Command path | Final speech result, confidence/command checks, a fresh target scan, exactly one eligible target, freshness/input-state checks, then Windows `SendInput` |
| Build | `source/build.ps1` with the Windows .NET Framework compiler; no `.sln` or `.csproj` is described |
| Diagnostics | Voice-only mode, OCR/filter tests, local practice controls, browser practice, and a local mouse integration test |

These are descriptions in the supplied documentation, not independently verified findings from reviewing the missing C# implementation.

The [known-issues document](https://github.com/EverForgeLabsHQ/EverForge-Community/blob/fd9bf368516b419b9cef1bf591d23c7ff61ec97a/Action%20Jackson%20Source/03%20KNOWN%20ISSUES.txt) explicitly records:

- Combined labels such as `Check/Fold` and `Call Any` are intentionally rejected by the current filtering logic.
- Some small/light OCR fixtures fail. The document reports 29 passing checks and four failures; this review did not rerun them.
- Recognition of “Fold” remained intermittently unreliable.
- Ambiguous targets, stale targets, held inputs, cooldown, or low confidence can prevent a click.
- Sending mouse input is not followed by confirmation that the target application accepted the action.
- The special local integration-test path does not establish all production detection or self-exclusion behavior.

The [build/test guide](https://github.com/EverForgeLabsHQ/EverForge-Community/blob/fd9bf368516b419b9cef1bf591d23c7ff61ec97a/Action%20Jackson%20Source/02%20BUILD%20AND%20TEST.txt) and [handoff verification](https://github.com/EverForgeLabsHQ/EverForge-Community/blob/fd9bf368516b419b9cef1bf591d23c7ff61ec97a/Action%20Jackson%20Source/HANDOFF%20VERIFICATION.txt) distinguish compilation from runtime compatibility. That distinction should be preserved during follow-up work.

## 2. What is actually published at the reviewed commit

The complete [recursive Git tree](https://api.github.com/repos/EverForgeLabsHQ/EverForge-Community/git/trees/c68eae95dc62a8cecad24f0300b7bfba15d79be8?recursive=1) was compared with [FILE INVENTORY.csv](https://github.com/EverForgeLabsHQ/EverForge-Community/blob/fd9bf368516b419b9cef1bf591d23c7ff61ec97a/Action%20Jackson%20Source/FILE%20INVENTORY.csv), normalizing Windows path separators.

| Measure | Count |
| --- | ---: |
| Files listed in the inventory, which excludes itself | 46 |
| Expected handoff total including the inventory | 47 |
| Files actually present under `Action Jackson Source/`, including the inventory | 10 |
| Inventory entries present | 9 |
| Inventory entries absent | **37** |

The ten published files are:

- `00 START HERE - SOURCE HANDOFF.txt`
- `01 CODE GUIDE.txt`
- `02 BUILD AND TEST.txt`
- `03 KNOWN ISSUES.txt`
- `Action Jackson.exe`
- `FILE INVENTORY.csv`
- `HANDOFF VERIFICATION.txt`
- `ICON-PROMPT.txt`
- `browser-practice.html`
- `libgcc_s_seh-1.dll`

This establishes path presence and absence. The inventory contains SHA-256 values, but this review has **not** independently downloaded and hash-validated the executable, DLLs, or model files.

## 3. Missing-file groups and impact

| Missing group | Count | Impact |
| --- | ---: | --- |
| `source/`: eight C# files, `app.manifest`, and `build.ps1` | 10 | Blocks implementation review and a source rebuild from this snapshot |
| `vosk-model-small-en-us-0.15/` | 14 | Prevents reproducing the documented bundled Vosk configuration |
| `libvosk.dll`, `libstdc++-6.dll`, `libwinpthread-1.dll` | 3 | Additional missing components of that Vosk configuration |
| `licenses/` | 4 | Referenced dependency license records are unavailable in the snapshot |
| `soldier.png`, `soldier.ico` | 2 | Build artwork referenced by the handoff is absent; the build guide describes regenerating the icon from the PNG |
| `START HERE.txt`, `THIRD-PARTY.txt`, `VALIDATION.txt` | 3 | Original instructions, dependency provenance, and the underlying prior validation record are unavailable |
| `microphone.txt` | 1 | Manifest difference involving a local device preference; publishing machine-specific settings is not required to unblock source review |

The ten source/build files total **63,255 bytes according to the inventory**. Uploading that small directory first would allow a focused code review while the larger runtime assets are arranged.

The missing Vosk files do **not** establish that the executable cannot launch or do anything: the guide describes a Windows Speech fallback. They establish that the documented Vosk setup cannot be reproduced from this snapshot alone.

<details>
<summary>Exact 37 inventory entries absent from the reviewed snapshot</summary>

All paths below are relative to `Action Jackson Source/`.

```text
libstdc++-6.dll
libvosk.dll
libwinpthread-1.dll
licenses/GCC-RUNTIME-EXCEPTION.txt
licenses/GPL-3.0.txt
licenses/MINGW-W64.txt
licenses/VOSK-APACHE-2.0.txt
microphone.txt
soldier.ico
soldier.png
source/App.cs
source/app.manifest
source/AssemblyInfo.cs
source/build.ps1
source/Detector.cs
source/Native.cs
source/OfflineVoice.cs
source/RoundedButton.cs
source/Tests.cs
source/Voice.cs
START HERE.txt
THIRD-PARTY.txt
VALIDATION.txt
vosk-model-small-en-us-0.15/am/final.mdl
vosk-model-small-en-us-0.15/conf/mfcc.conf
vosk-model-small-en-us-0.15/conf/model.conf
vosk-model-small-en-us-0.15/graph/disambig_tid.int
vosk-model-small-en-us-0.15/graph/Gr.fst
vosk-model-small-en-us-0.15/graph/HCLr.fst
vosk-model-small-en-us-0.15/graph/phones/word_boundary.int
vosk-model-small-en-us-0.15/ivector/final.dubm
vosk-model-small-en-us-0.15/ivector/final.ie
vosk-model-small-en-us-0.15/ivector/final.mat
vosk-model-small-en-us-0.15/ivector/global_cmvn.stats
vosk-model-small-en-us-0.15/ivector/online_cmvn.conf
vosk-model-small-en-us-0.15/ivector/splice.conf
vosk-model-small-en-us-0.15/README
```

</details>

## 4. Requested additions

- [ ] Publish the complete `source/` directory, including the eight C# files, `app.manifest`, and `build.ps1`.
- [ ] Supply the build artwork referenced by the script, or document how the required artwork is obtained/generated.
- [ ] Add `VALIDATION.txt`, the original instructions, and dependency provenance/license records referenced by the handoff.
- [ ] Provide the exact speech model and native runtime dependencies through a complete release bundle or reproducible acquisition instructions with versions and checksums. These do not all need to be committed as repository source files.
- [ ] Reconcile the inventory and verification notes with the published layout. Mark generated files and machine-specific preferences explicitly if they are intentionally excluded.
- [ ] Once the handoff is reproducible, record a fresh build and local diagnostic results, retaining known failing cases and distinguishing them from tests not attempted.

## 5. Focus for the subsequent implementation review

Once the source is available, the useful first review is to trace where a command stops: finalized speech, command acceptance, target count, filtering reason, freshness/input checks, and input delivery. Any confirmation of the target application's result should be recorded separately.

The existing local practice modes offer a starting point for isolating those stages. This review does not claim that an OCR problem, a speech problem, an input restriction, or an AI moderation decision has been established as the cause of the original live-site behavior.

