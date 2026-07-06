// ═════════════════════════════════════════════════════════════════════════════
//  GnomesSetupWizard.cs — Editor only
//  Tools/Gnomes/Project Setup
//
//  What this generates:
//  - Intent fields injected directly into existing module .cs files,
//    between WIZARD-BEGIN / WIZARD-END marker comments
//  - ResetIntents() body injected between WIZARD-BEGIN-RESET / WIZARD-END-RESET
//  - One InputLinker .asset per mapped action (reused if already exists)
//  - Linker assets wired to the PlayerBrain asset automatically
//
//  No IntentDefinition class. No GnomesConfig. No separate interface files.
//  Intents live on the module they belong to.
// ═════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GNOMES.Actor.Core;
using GNOMES.Input;
using GNOMES.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR

namespace GNOMES.Editor
{
    public class GnomesSetupWizard : EditorWindow
    {
        // ── Tabs ──────────────────────────────────────────────────────────────

        private enum Tab { Assets, MapActions, Output, Preview }

        private static readonly string[] TabLabels =
            { "1 · Assets", "2 · Map Actions", "3 · Output", "4 · Preview & Generate" };

        private Tab _activeTab = Tab.Assets;

        // ── Persistent window state ───────────────────────────────────────────
        // [SerializeField] survives domain reloads (e.g. after AssetDatabase.Refresh)

        [SerializeField] private InputActionAsset _inputAsset;
        [SerializeField] private PlayerBrain      _playerBrain;
        [SerializeField] private string           _outputPath = "Assets/Scripts/Generated";
        [SerializeField] private string           _linkerPath = "Assets/Settings/InputLinkers";

        // ── Binding rows ──────────────────────────────────────────────────────

        private readonly List<BindingRow> _rows       = new();
        private          Vector2          _rowsScroll = Vector2.zero;

        // Module dropdown — populated in Initialize() from IBrainModule subtypes
        private string[] _moduleTypeNames;   // fully-qualified, e.g. "...LocomotionModule"
        private string[] _moduleLabels;      // short display name

        // Field names already written to disk (between wizard markers)
        // Used to show ★ badges on new rows
        private readonly HashSet<string> _previousFieldNames = new();

        // ── New module form state ─────────────────────────────────────────────

        private bool   _showNewModuleForm  = false;
        private string _newModuleName      = "MyCustomModule";
        private string _newModuleNamespace = "Gnomes.Actor.Modules";

        // ── Cached styles ─────────────────────────────────────────────────────
        // Allocated once, never per-frame — avoids GC pressure during scroll

        private GUIStyle _styleTabActive;
        private GUIStyle _styleTabInactive;
        private GUIStyle _styleTabBlocked;
        private GUIStyle _styleHeader;
        private GUIStyle _styleGenerateButton;
        private GUIStyle _styleLinkerExists;
        private GUIStyle _styleLinkerMissing;
        private bool     _stylesDirty = true;

        // ── Cached preview ────────────────────────────────────────────────────
        // Rebuilt only when row state changes, never per-frame

        private string _previewText        = "";
        private int    _previewMappedCount = 0;
        private int    _previewNewCount    = 0;
        private bool   _previewDirty       = true;
        private int    _lastRowHash        = 0;

        // ── Entry point ───────────────────────────────────────────────────────

        [MenuItem("Tools/Gnomes/Project Setup")]
        public static void Open()
        {
            var w     = GetWindow<GnomesSetupWizard>("⬡  Gnomes Setup");
            w.minSize = new Vector2(700, 500);
            w.Show();
        }

        // OnEnable fires on first open AND after every domain reload,
        // so the module list and rows stay current after recompiles.
        private void OnEnable() => Initialize();

        // ── Initialize ────────────────────────────────────────────────────────

        private void Initialize()
        {
            DiscoverModules();
            LoadExistingFieldNames();

            // Rows are plain C# objects — wiped on domain reload.
            // Rebuild from the still-serialized _inputAsset if present.
            if (_inputAsset != null && _rows.Count == 0)
                RebuildRows();

            _previewDirty = true;
        }

        /// <summary>
        /// Reflects over all loaded assemblies to find every concrete
        /// IBrainModule subtype. Framework modules and user-defined modules
        /// both appear here automatically.
        /// </summary>
        private void DiscoverModules()
        {
            var types = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t =>
                    !t.IsAbstract &&
                    !t.IsInterface &&
                    typeof(IBrainModule).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToArray();

            _moduleTypeNames = types
                .Select(t => t.FullName)
                .Prepend("")
                .ToArray();

            _moduleLabels = _moduleTypeNames
                .Select(n => string.IsNullOrEmpty(n)
                    ? "— unmapped —"
                    : n.Split('.').Last())
                .ToArray();
        }

        /// <summary>
        /// Scans .cs files in the output folder for intent fields between
        /// WIZARD-BEGIN / WIZARD-END markers. Populates _previousFieldNames
        /// so new rows can be highlighted with ★.
        /// </summary>
        private void LoadExistingFieldNames()
        {
            _previousFieldNames.Clear();
            if (!Directory.Exists(_outputPath)) return;

            foreach (var file in Directory.GetFiles(_outputPath, "*.cs"))
            {
                bool inside = false;
                foreach (var line in File.ReadAllLines(file))
                {
                    if (line.Contains("WIZARD-BEGIN"))  { inside = true;  continue; }
                    if (line.Contains("WIZARD-END"))    { inside = false; continue; }
                    if (!inside) continue;

                    var t = line.Trim();
                    // Matches: "public IntentValue<Vector2> Move { get; }"
                    //      or: "public IntentTrigger Jump { get; }"
                    if (!t.StartsWith("public Intent")) continue;
                    var parts = t.Split(' ');
                    if (parts.Length >= 3)
                        _previousFieldNames.Add(parts[2].Trim(';', '{', ' '));
                }
            }

            _previewDirty = true;
        }

        // ── Style cache ───────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (!_stylesDirty) return;
            _stylesDirty = false;

            _styleTabActive = new GUIStyle(EditorStyles.toolbarButton)
                { fontStyle = FontStyle.Bold, fontSize = 11 };
            _styleTabActive.normal.textColor  = new Color(0.22f, 0.78f, 0.93f);
            _styleTabActive.focused.textColor = new Color(0.22f, 0.78f, 0.93f);

            _styleTabInactive = new GUIStyle(EditorStyles.toolbarButton)
                { fontSize = 11 };

            _styleTabBlocked = new GUIStyle(EditorStyles.toolbarButton)
                { fontSize = 11 };
            _styleTabBlocked.normal.textColor = new Color(0.4f, 0.4f, 0.4f);

            _styleHeader = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 13, alignment = TextAnchor.MiddleLeft };

            _styleGenerateButton = new GUIStyle(GUI.skin.button)
                { fontSize = 13, fontStyle = FontStyle.Bold };

            _styleLinkerExists = new GUIStyle(EditorStyles.miniLabel);
            _styleLinkerExists.normal.textColor = new Color(0.4f, 0.8f, 0.4f);

            _styleLinkerMissing = new GUIStyle(EditorStyles.miniLabel);
            _styleLinkerMissing.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
        }

        // ── Preview cache ─────────────────────────────────────────────────────

        private void RebuildPreviewIfDirty()
        {
            int hash = 0;
            foreach (var row in _rows)
                hash = HashCode.Combine(
                    hash,
                    row.TargetModuleTypeName?.GetHashCode() ?? 0,
                    row.FieldName?.GetHashCode() ?? 0,
                    row.ActionType?.GetHashCode() ?? 0);

            if (hash == _lastRowHash && !_previewDirty) return;
            _lastRowHash  = hash;
            _previewDirty = false;

            var mapped = MappedRows();
            _previewMappedCount = mapped.Count;
            _previewNewCount    = mapped.Count(r =>
                !_previousFieldNames.Contains(r.FieldName));

            if (mapped.Count == 0) { _previewText = ""; return; }

            var sb = new StringBuilder();
            foreach (var grp in mapped
                .GroupBy(r => r.TargetModuleTypeName)
                .OrderBy(g => g.Key))
            {
                sb.AppendLine($"  {grp.Key.Split('.').Last()}");
                foreach (var row in grp)
                {
                    bool isNew = !_previousFieldNames.Contains(row.FieldName);
                    sb.AppendLine(
                        $"    {(isNew ? "★ " : "  ")}" +
                        $"{IntentTypeName(row.ActionType),28}  {row.FieldName}" +
                        $"   →  {row.ActionName}Linker.asset");
                }
            }
            _previewText = sb.ToString();
        }

        // ── OnGUI ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            EnsureStyles();
            RebuildPreviewIfDirty();
            DrawTabBar();
            DrawSeparator();
            GUILayout.Space(8);

            switch (_activeTab)
            {
                case Tab.Assets:     DrawTabAssets();     break;
                case Tab.MapActions: DrawTabMapActions(); break;
                case Tab.Output:     DrawTabOutput();     break;
                case Tab.Preview:    DrawTabPreview();    break;
            }
        }

        // ── Tab bar ───────────────────────────────────────────────────────────

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            for (int i = 0; i < TabLabels.Length; i++)
            {
                var  tab     = (Tab)i;
                bool active  = _activeTab == tab;
                bool blocked = IsTabBlocked(tab);

                var style = active  ? _styleTabActive
                          : blocked ? _styleTabBlocked
                          :           _styleTabInactive;

                EditorGUI.BeginDisabledGroup(blocked);
                if (GUILayout.Toggle(active, TabLabels[i], style,
                    GUILayout.ExpandWidth(true)) && !active)
                    _activeTab = tab;
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool IsTabBlocked(Tab tab) => tab switch
        {
            Tab.MapActions => _inputAsset == null,
            Tab.Output     => _inputAsset == null,
            Tab.Preview    => _inputAsset == null || _previewMappedCount == 0,
            _              => false
        };

        // ── Tab: Assets ───────────────────────────────────────────────────────

        private void DrawTabAssets()
        {
            DrawTabHeader("Assets",
                "Assign your .inputactions asset and optionally a PlayerBrain. " +
                "The PlayerBrain will be automatically wired with generated linker assets.");

            EditorGUI.BeginChangeCheck();
            _inputAsset = (InputActionAsset)EditorGUILayout.ObjectField(
                "Input Actions Asset", _inputAsset, typeof(InputActionAsset), false);
            if (EditorGUI.EndChangeCheck() && _inputAsset != null)
            {
                RebuildRows();
                _activeTab = Tab.MapActions;
            }

            GUILayout.Space(4);

            _playerBrain = (PlayerBrain)EditorGUILayout.ObjectField(
                "Player Brain (optional)", _playerBrain, typeof(PlayerBrain), false);

            if (_playerBrain == null)
                EditorGUILayout.HelpBox(
                    "Without a PlayerBrain, linker assets are created but not wired. " +
                    "Assign the brain and regenerate at any time.",
                    MessageType.None);

            GUILayout.FlexibleSpace();
            DrawNextButton(Tab.MapActions, _inputAsset != null);
        }

        // ── Tab: Map Actions ──────────────────────────────────────────────────

        private void DrawTabMapActions()
        {
            DrawTabHeader("Map Actions",
                "Map each input action to a module and field name. " +
                "The wizard injects the intent field directly into that module's .cs file " +
                "between its WIZARD-BEGIN / WIZARD-END markers.");

            if (_moduleTypeNames.Length <= 1)
            {
                EditorGUILayout.HelpBox(
                    "No IBrainModule types found in the project. " +
                    "Create one below to get started.",
                    MessageType.Warning);
            }
            else
            {
                GUILayout.Space(4);
                DrawMappingColumnHeaders();
                DrawSeparator();

                _rowsScroll = EditorGUILayout.BeginScrollView(_rowsScroll);
                foreach (var row in _rows)
                    DrawBindingRow(row);
                EditorGUILayout.EndScrollView();

                DrawSeparator();
                GUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                int mapped = MappedRows().Count;
                EditorGUILayout.LabelField(
                    $"{mapped} of {_rows.Count} actions mapped",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                DrawNextButton(Tab.Output, mapped > 0);
                EditorGUILayout.EndHorizontal();
            }

            // ── New module form ───────────────────────────────────────────────
            GUILayout.Space(4);
            DrawSeparator();
            GUILayout.Space(4);

            if (!_showNewModuleForm)
            {
                if (GUILayout.Button(
                    "＋  New Module",
                    GUILayout.Height(22), GUILayout.Width(120)))
                {
                    _showNewModuleForm = true;
                    _newModuleName     = "MyCustomModule";
                }
            }
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("New Module", EditorStyles.boldLabel);
                GUILayout.Space(4);

                _newModuleName      = EditorGUILayout.TextField("Name",      _newModuleName);
                _newModuleNamespace = EditorGUILayout.TextField("Namespace", _newModuleNamespace);

                EditorGUILayout.HelpBox(
                    "Generates a new GnomesModule subclass with WIZARD-BEGIN / WIZARD-END " +
                    "markers pre-placed. Add your state fields (ObservableValue etc.) below " +
                    "the markers manually after creation.",
                    MessageType.None);

                var formErrors = ValidateNewModuleName(_newModuleName);
                foreach (var e in formErrors)
                    EditorGUILayout.HelpBox(e, MessageType.Error);

                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginDisabledGroup(formErrors.Count > 0);
                if (GUILayout.Button("Create & Open", GUILayout.Height(24)))
                {
                    CreateModule(_newModuleName, _newModuleNamespace);
                    _showNewModuleForm = false;
                }
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Cancel", GUILayout.Height(24)))
                    _showNewModuleForm = false;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        private List<string> ValidateNewModuleName(string name)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
            { errors.Add("Name cannot be empty."); return errors; }

            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9]*$"))
                errors.Add("Must be a valid C# identifier.");

            if (!name.EndsWith("Module"))
                errors.Add("Name should end with 'Module' (e.g. VehicleModule).");

            string filePath = Path.Combine(_outputPath, $"{name}.cs");
            if (File.Exists(filePath))
                errors.Add($"{name}.cs already exists at {_outputPath}.");

            if (_moduleTypeNames?.Any(t => t.Split('.').Last() == name) == true)
                errors.Add($"{name} is already loaded in the project.");

            return errors;
        }

        private void CreateModule(string moduleName, string namespaceName)
        {
            Directory.CreateDirectory(_outputPath);
            string filePath = Path.Combine(_outputPath, $"{moduleName}.cs");

            var sb = new StringBuilder();
            sb.AppendLine("// Created by Gnomes Setup Wizard");
            sb.AppendLine("// Add your state fields below the WIZARD-END marker.");
            sb.AppendLine("// Do not edit between the WIZARD markers manually.");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using GNOMES.Input;");
            sb.AppendLine("using GNOMES.Modules;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.AppendLine($"    public class {moduleName} : GnomesModule");
            sb.AppendLine("    {");
            sb.AppendLine($"        // ── Intent fields (wizard-managed) ───────────────────────────────");
            sb.AppendLine($"        // WIZARD-BEGIN:{moduleName}");
            sb.AppendLine($"        // WIZARD-END:{moduleName}");
            sb.AppendLine();
            sb.AppendLine($"        // ── State fields (add yours here) ────────────────────────────────");
            sb.AppendLine($"        // e.g. public ObservableValue<Vector3> Velocity = new();");
            sb.AppendLine();
            sb.AppendLine("        public override void ResetIntents()");
            sb.AppendLine("        {");
            sb.AppendLine($"            // WIZARD-BEGIN-RESET:{moduleName}");
            sb.AppendLine($"            // WIZARD-END-RESET:{moduleName}");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public override void Reset()");
            sb.AppendLine("        {");
            sb.AppendLine("            ResetIntents();");
            sb.AppendLine("            // Clear your state ObservableValues here");
            sb.AppendLine("            // e.g. Velocity.SetWithoutNotify(Vector3.zero);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString());
            AssetDatabase.Refresh();

            UnityEditorInternal.InternalEditorUtility
                .OpenFileAtLineExternal(filePath, 17);

            Debug.Log(
                $"[Gnomes] Created {moduleName}.cs at {filePath}.");
        }

        private void DrawMappingColumnHeaders()
        {
            var rect = EditorGUILayout.GetControlRect(false, 18);
            float x  = rect.x + 4;
            EditorGUI.LabelField(new Rect(x,       rect.y, 20,  18), "",           EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(x + 22,  rect.y, 150, 18), "Action",     EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(x + 178, rect.y, 68,  18), "Type",       EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(x + 252, rect.y, 178, 18), "Module",     EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(x + 436, rect.y, 158, 18), "Field Name", EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(x + 600, rect.y, 80,  18), "Linker",     EditorStyles.miniLabel);
        }

        private void DrawBindingRow(BindingRow row)
        {
            var rowRect = EditorGUILayout.GetControlRect(false, 22);
            float x     = rowRect.x + 4;

            bool isNew = !string.IsNullOrEmpty(row.TargetModuleTypeName) &&
                         !_previousFieldNames.Contains(row.FieldName);

            if (isNew)
                EditorGUI.DrawRect(
                    new Rect(rowRect.x, rowRect.y, rowRect.width, 22),
                    new Color(0.22f, 0.78f, 0.93f, 0.07f));

            EditorGUI.LabelField(
                new Rect(x, rowRect.y + 2, 20, 18),
                isNew ? new GUIContent("★", "New — not yet generated") : GUIContent.none,
                EditorStyles.miniLabel);

            EditorGUI.LabelField(new Rect(x + 22,  rowRect.y, 150, 20), row.ActionName);
            EditorGUI.LabelField(new Rect(x + 178, rowRect.y, 68,  20),
                row.ActionType, EditorStyles.miniLabel);

            // Module dropdown
            int cur = Array.IndexOf(_moduleTypeNames, row.TargetModuleTypeName);
            if (cur < 0) cur = 0;
            int sel = EditorGUI.Popup(
                new Rect(x + 252, rowRect.y, 178, 18), cur, _moduleLabels);
            row.TargetModuleTypeName = _moduleTypeNames[sel];

            row.FieldName = EditorGUI.TextField(
                new Rect(x + 436, rowRect.y, 158, 18), row.FieldName);

            EditorGUI.LabelField(
                new Rect(x + 600, rowRect.y, 90, 20),
                row.LinkerExists ? "✓ exists" : "will create",
                row.LinkerExists ? _styleLinkerExists : _styleLinkerMissing);
        }

        // ── Tab: Output ───────────────────────────────────────────────────────

        private void DrawTabOutput()
        {
            DrawTabHeader("Output Settings",
                "Configure where to look for module source files and where to create linker assets.");

            GUILayout.Space(4);

            _outputPath = DrawFolderField("Module Source Folder", _outputPath);
            _linkerPath = DrawFolderField("Linker Assets Folder", _linkerPath);

            GUILayout.Space(12);

            var mapped = MappedRows();
            if (mapped.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Intent fields will be injected into:");
                foreach (var grp in mapped
                    .GroupBy(r => r.TargetModuleTypeName)
                    .OrderBy(g => g.Key))
                {
                    int count = grp.Count();
                    sb.AppendLine(
                        $"  {grp.Key.Split('.').Last()}.cs  " +
                        $"({count} field{(count == 1 ? "" : "s")})");
                }
                sb.AppendLine();
                sb.AppendLine($"Linker assets  →  {_linkerPath}/");
                EditorGUILayout.HelpBox(sb.ToString(), MessageType.None);
            }

            GUILayout.FlexibleSpace();
            DrawNextButton(Tab.Preview, true);
        }

        // ── Tab: Preview & Generate ───────────────────────────────────────────

        private void DrawTabPreview()
        {
            DrawTabHeader("Preview & Generate",
                "Review what will change, then click Generate.");

            GUILayout.Space(4);

            if (string.IsNullOrEmpty(_previewText))
                EditorGUILayout.HelpBox(
                    "No actions mapped — go back to Map Actions.",
                    MessageType.Warning);
            else
                EditorGUILayout.HelpBox(_previewText, MessageType.None);

            GUILayout.Space(8);

            var errors = Validate();
            foreach (var e in errors)
                EditorGUILayout.HelpBox(e, MessageType.Error);

            GUILayout.FlexibleSpace();
            DrawSeparator();
            GUILayout.Space(6);

            EditorGUI.BeginDisabledGroup(errors.Count > 0 || _previewMappedCount == 0);
            string btnLabel = _previewNewCount > 0
                ? $"Generate  ({_previewMappedCount} intents, {_previewNewCount} new)"
                : $"Regenerate  ({_previewMappedCount} intents, no changes)";
            if (GUILayout.Button(btnLabel, _styleGenerateButton, GUILayout.Height(40)))
                Generate();
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(8);
        }

        // ── Validation ────────────────────────────────────────────────────────

        private List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(_outputPath))
                errors.Add("Module source folder cannot be empty.");
            if (string.IsNullOrWhiteSpace(_linkerPath))
                errors.Add("Linker assets folder cannot be empty.");

            var mapped = MappedRows();

            // Duplicate field names within the same module
            foreach (var grp in mapped.GroupBy(r => r.TargetModuleTypeName))
            {
                foreach (var dupe in grp
                    .GroupBy(r => r.FieldName)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key))
                    errors.Add(
                        $"Duplicate field '{dupe}' in " +
                        $"{grp.Key.Split('.').Last()}.");
            }

            foreach (var row in mapped)
                if (string.IsNullOrWhiteSpace(row.FieldName))
                    errors.Add($"'{row.ActionName}' has no field name.");

            // Warn if a module .cs file can't be found — inject will be skipped
            foreach (var grp in mapped.GroupBy(r => r.TargetModuleTypeName))
            {
                string moduleName = grp.Key.Split('.').Last();
                string path = FindModuleFile(moduleName);
                if (path == null)
                    errors.Add(
                        $"Cannot find {moduleName}.cs in '{_outputPath}'. " +
                        $"Check the Module Source Folder path.");
            }

            return errors;
        }

        // ── Generation ────────────────────────────────────────────────────────

        private void Generate()
        {
            var mapped = MappedRows();

            InjectIntentFields(mapped);
            GenerateLinkerAssets(mapped);

            AssetDatabase.Refresh();
            LoadExistingFieldNames();
            RefreshLinkerStatus();

            int newCount = mapped.Count(r =>
                !_previousFieldNames.Contains(r.FieldName));

            Debug.Log(
                $"[Gnomes] Generation complete — {mapped.Count} intents, " +
                $"{newCount} new.");

            EditorUtility.DisplayDialog(
                "Gnomes — Generation Complete",
                $"Generated {mapped.Count} intent(s).\n\n" +
                $"• Intent fields injected into module source files\n" +
                $"• {mapped.Count} linker asset(s) created/updated\n" +
                (_playerBrain != null
                    ? $"• PlayerBrain '{_playerBrain.name}' updated"
                    : "• No PlayerBrain assigned — wire linkers manually"),
                "Done");
        }

        // ── Intent field injection ────────────────────────────────────────────

        /// <summary>
        /// For each module that has mapped rows, finds the module's .cs file
        /// and injects / updates the intent fields and ResetIntents() body
        /// between the WIZARD-BEGIN/WIZARD-END marker pairs.
        /// </summary>
        private void InjectIntentFields(List<BindingRow> mapped)
        {
            foreach (var grp in mapped
                .GroupBy(r => r.TargetModuleTypeName)
                .OrderBy(g => g.Key))
            {
                string moduleName = grp.Key.Split('.').Last();
                string filePath   = FindModuleFile(moduleName);

                if (filePath == null)
                {
                    Debug.LogError(
                        $"[Gnomes] Cannot find {moduleName}.cs in '{_outputPath}'. " +
                        $"Intent fields not injected.");
                    continue;
                }

                var rows    = grp.ToList();
                var content = File.ReadAllText(filePath);

                // ── Inject property declarations ──────────────────────────────
                content = InjectBetweenMarkers(
                    content,
                    $"WIZARD-BEGIN:{moduleName}",
                    $"WIZARD-END:{moduleName}",
                    BuildPropertyBlock(rows));

                // ── Inject ResetIntents() body ────────────────────────────────
                content = InjectBetweenMarkers(
                    content,
                    $"WIZARD-BEGIN-RESET:{moduleName}",
                    $"WIZARD-END-RESET:{moduleName}",
                    BuildResetBlock(rows));

                File.WriteAllText(filePath, content);
            }
        }

        /// <summary>
        /// Replaces everything between the start and end marker lines
        /// (exclusive) with <paramref name="newContent"/>.
        /// If the markers are not found, logs a warning and returns the
        /// original content unchanged.
        /// </summary>
        private static string InjectBetweenMarkers(
            string content, string beginMarker, string endMarker,
            string newContent)
        {
            string pattern =
                $@"([ \t]*//[ \t]*{Regex.Escape(beginMarker)}[^\n]*\n)" +
                $@"(?:.*?\n)*?" +
                $@"([ \t]*//[ \t]*{Regex.Escape(endMarker)}[^\n]*)";

            var match = Regex.Match(content, pattern,
                RegexOptions.Singleline | RegexOptions.Multiline);

            if (!match.Success)
            {
                Debug.LogWarning(
                    $"[Gnomes] Markers '{beginMarker}' / '{endMarker}' not found. " +
                    $"Add them to the module file.");
                return content;
            }

            // Preserve the indentation of the begin marker
            string indent = Regex.Match(match.Groups[1].Value, @"^([ \t]*)").Groups[1].Value;
            string indented = string.Join("\n",
                newContent
                    .Split('\n')
                    .Select(l => string.IsNullOrWhiteSpace(l) ? l : indent + l));

            return content.Substring(0, match.Groups[1].Index + match.Groups[1].Length) +
                   indented + "\n" +
                   content.Substring(match.Groups[2].Index);
        }

        private static string BuildPropertyBlock(List<BindingRow> rows)
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                string typeName = IntentTypeName(row.ActionType);
                string backing  = LowerFirst(row.FieldName);
                sb.AppendLine(
                    $"private readonly {typeName} _{backing} = new();");
                sb.AppendLine(
                    $"public {typeName} {row.FieldName} => _{backing};");
            }
            return sb.ToString().TrimEnd();
        }

        private static string BuildResetBlock(List<BindingRow> rows)
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                string backing = LowerFirst(row.FieldName);
                if (row.ActionType == "Button")
                    sb.AppendLine($"_{backing}.ClearSubscribers();");
                else
                    sb.AppendLine($"_{backing}.Value = default;");
            }
            return sb.ToString().TrimEnd();
        }

        // ── Linker asset generation ───────────────────────────────────────────

        private void GenerateLinkerAssets(List<BindingRow> mapped)
        {
            Directory.CreateDirectory(_linkerPath);
            var created = new List<InputLinker>();

            foreach (var row in mapped)
            {
                string      path   = GetLinkerAssetPath(row);
                InputLinker linker = AssetDatabase.LoadAssetAtPath<InputLinker>(path);

                if (linker == null)
                {
                    linker = CreateInstance(LinkerType(row.ActionType)) as InputLinker;
                    AssetDatabase.CreateAsset(linker, path);
                }

                // Always sync fields in case the wizard config changed
                var so = new SerializedObject(linker);
                so.FindProperty("ActionName")         ?.SetString(row.ActionName);
                so.FindProperty("IntentFieldName")    ?.SetString(row.FieldName);
                so.FindProperty("TargetModuleTypeName")?.SetString(row.TargetModuleTypeName);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(linker);
                created.Add(linker);

                row.LinkerExists = true;
            }

            AssetDatabase.SaveAssets();

            if (_playerBrain == null) return;

            var brainSo = new SerializedObject(_playerBrain);
            var prop    = brainSo.FindProperty("Linkers");
            if (prop == null) return;

            var existing = new HashSet<UnityEngine.Object>();
            for (int i = 0; i < prop.arraySize; i++)
                existing.Add(prop.GetArrayElementAtIndex(i).objectReferenceValue);

            foreach (var linker in created)
            {
                if (existing.Contains(linker)) continue;
                prop.arraySize++;
                prop.GetArrayElementAtIndex(prop.arraySize - 1)
                    .objectReferenceValue = linker;
            }

            brainSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(_playerBrain);
        }

        // ── Row helpers ───────────────────────────────────────────────────────

        private List<BindingRow> MappedRows() =>
            _rows.Where(r => !string.IsNullOrEmpty(r.TargetModuleTypeName)).ToList();

        private void RebuildRows()
        {
            _rows.Clear();
            if (_inputAsset == null) return;

            foreach (var map in _inputAsset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    if (_rows.Any(r => r.ActionName == action.name)) continue;
                    _rows.Add(new BindingRow
                    {
                        ActionName           = action.name,
                        ActionType           = InferActionType(action),
                        FieldName            = action.name,
                        TargetModuleTypeName = ""
                    });
                }
            }

            RefreshLinkerStatus();
        }

        private void RefreshLinkerStatus()
        {
            string cwd = Directory.GetCurrentDirectory();
            foreach (var row in _rows)
            {
                string full = Path.Combine(cwd,
                    GetLinkerAssetPath(row).Replace('/', Path.DirectorySeparatorChar));
                row.LinkerExists = File.Exists(full);
            }
        }

        // ── File helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Searches _outputPath recursively for a .cs file whose name matches
        /// the module type name (e.g. "LocomotionModule.cs").
        /// </summary>
        private string FindModuleFile(string moduleName)
        {
            if (!Directory.Exists(_outputPath)) return null;

            var matches = Directory.GetFiles(
                _outputPath, $"{moduleName}.cs", SearchOption.AllDirectories);

            return matches.FirstOrDefault();
        }

        private string GetLinkerAssetPath(BindingRow row) =>
            Path.Combine(_linkerPath, $"{row.ActionName}Linker.asset")
                .Replace('\\', '/');

        // ── Shared helpers ────────────────────────────────────────────────────

        private static Type LinkerType(string actionType) => actionType switch
        {
            "Vector2" => typeof(Vector2InputLinker),
            "Float"   => typeof(FloatInputLinker),
            _         => typeof(ButtonInputLinker)
        };

        private static string InferActionType(InputAction action) =>
            action.expectedControlType switch
            {
                "Vector2" => "Vector2",
                "Axis"    => "Float",
                _         => "Button"
            };

        private static string IntentTypeName(string actionType) => actionType switch
        {
            "Vector2" => "IntentValue<UnityEngine.Vector2>",
            "Float"   => "IntentValue<float>",
            _         => "IntentTrigger"
        };

        private static string LowerFirst(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToLower(s[0]) + s[1..];

        private string DrawFolderField(string label, string current)
        {
            EditorGUILayout.BeginHorizontal();
            current = EditorGUILayout.TextField(label, current);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string picked = EditorUtility.OpenFolderPanel(label, current, "");
                if (!string.IsNullOrEmpty(picked) &&
                    picked.StartsWith(Application.dataPath))
                    current = "Assets" + picked[Application.dataPath.Length..];
            }
            EditorGUILayout.EndHorizontal();
            return current;
        }

        private void DrawTabHeader(string title, string description)
        {
            EditorGUILayout.LabelField(title, _styleHeader);
            GUILayout.Space(2);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(6);
            DrawSeparator();
            GUILayout.Space(8);
        }

        private void DrawNextButton(Tab nextTab, bool enabled)
        {
            EditorGUI.BeginDisabledGroup(!enabled);
            if (GUILayout.Button(
                $"Next  →  {TabLabels[(int)nextTab]}",
                GUILayout.Height(28)))
                _activeTab = nextTab;
            EditorGUI.EndDisabledGroup();
            GUILayout.Space(4);
        }

        private static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.28f, 0.28f, 0.30f));
        }

        // ── Data class ────────────────────────────────────────────────────────

        private class BindingRow
        {
            public string ActionName;
            public string ActionType;
            public string TargetModuleTypeName;
            public string FieldName;
            public bool   LinkerExists;
        }
    }

    // ── SerializedProperty extension ─────────────────────────────────────────

    internal static class SerializedPropertyExtensions
    {
        public static void SetString(this SerializedProperty prop, string value)
        {
            if (prop != null) prop.stringValue = value;
        }
    }
}

#endif