using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GNOMES.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;

namespace GNOMES.Editor {
// ── Shared style helpers ──────────────────────────────────────────────────────

    internal static class PolyStyle {
        public static readonly Color BgBase = new(0.17f, 0.17f, 0.18f, 1f);
        public static readonly Color BgRow = new(0.20f, 0.20f, 0.21f, 1f);
        public static readonly Color BgRowHover = new(0.24f, 0.24f, 0.26f, 1f);
        public static readonly Color BgRowSel = new(0.22f, 0.23f, 0.26f, 1f);
        public static readonly Color BgHeader = new(0.14f, 0.14f, 0.15f, 1f);
        public static readonly Color Accent = new(0.22f, 0.78f, 0.93f, 1f);
        public static readonly Color AccentDim = new(0.22f, 0.78f, 0.93f, 0.18f);
        public static readonly Color TextPrimary = new(0.92f, 0.92f, 0.94f, 1f);
        public static readonly Color TextMuted = new(0.55f, 0.55f, 0.60f, 1f);
        public static readonly Color Border = new(0.28f, 0.28f, 0.30f, 1f);
        public static readonly Color Separator = new(0.26f, 0.26f, 0.28f, 1f);
        private static readonly Color DangerDim = new(0.85f, 0.28f, 0.28f, 0.75f);
        private static readonly Color BadgeBg = new(0.28f, 0.28f, 0.32f, 1f);
        public static readonly Color DragHover = new(0.22f, 0.78f, 0.93f, 0.10f);
        public static readonly Color DropLine = new(0.22f, 0.78f, 0.93f, 1f);

        public static Button AccentButton(string label, Action clicked) {
            var btn = new Button(clicked) {
                text = label,
                style = {
                    backgroundColor = AccentDim,
                    color = Accent,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = Accent,
                    borderBottomColor = Accent,
                    borderLeftColor = Accent,
                    borderRightColor = Accent,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginLeft = 3,
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            SetHover(btn, AccentDim, new Color(0.22f, 0.78f, 0.93f, 0.32f));
            return btn;
        }

        public static Button GhostButton(string label, Action clicked) {
            var btn = new Button(clicked) {
                text = label,
                style = {
                    backgroundColor = new Color(0, 0, 0, 0),
                    color = TextMuted,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = Border,
                    borderBottomColor = Border,
                    borderLeftColor = Border,
                    borderRightColor = Border,
                    paddingLeft = 7,
                    paddingRight = 7,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginLeft = 3,
                    fontSize = 11
                }
            };
            return btn;
        }

        public static Button DangerButton(string label, Action clicked) {
            var btn = GhostButton(label, clicked);
            btn.style.color = DangerDim;
            btn.style.borderTopColor = DangerDim;
            btn.style.borderBottomColor = DangerDim;
            btn.style.borderLeftColor = DangerDim;
            btn.style.borderRightColor = DangerDim;
            return btn;
        }

        public static Label IndexBadge(int index) {
            var lbl = new Label($"{index}") {
                style = {
                    backgroundColor = BadgeBg,
                    color = TextMuted,
                    fontSize = 9,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    paddingLeft = 5,
                    paddingRight = 5,
                    paddingTop = 1,
                    paddingBottom = 1,
                    marginRight = 6,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    minWidth = 18
                }
            };
            return lbl;
        }

        public static void SetHover(VisualElement el, Color normal, Color hover) {
            el.RegisterCallback<MouseEnterEvent>(_ => el.style.backgroundColor = hover);
            el.RegisterCallback<MouseLeaveEvent>(_ => el.style.backgroundColor = normal);
        }
    }

// ── Field-migration helper ────────────────────────────────────────────────────

    /// <summary>
    /// Copies field values from oldValue into a new instance of
    /// newType. Only fields whose name AND type both match are
    /// carried over; everything else stays at the new type's default.
    /// </summary>
    internal static class PolyMigration {
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> Cache = new();

        private static Dictionary<string, FieldInfo> GetFieldMap(Type type) {
            if (Cache.TryGetValue(type, out var map)) return map;
            map = new Dictionary<string, FieldInfo>();
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                              BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    map.TryAdd(f.Name, f);
            Cache[type] = map;
            return map;
        }

        public static object MigrateInto(object oldValue, Type newType) {
            var newInstance = Activator.CreateInstance(newType);
            if (oldValue == null) return newInstance;

            var srcMap = GetFieldMap(oldValue.GetType());
            var dstMap = GetFieldMap(newType);

            foreach (var (name, dstField) in dstMap) {
                if (!srcMap.TryGetValue(name, out var srcField)) continue;
                if (srcField.FieldType != dstField.FieldType) continue;
                dstField.SetValue(newInstance, srcField.GetValue(oldValue));
            }

            return newInstance;
        }
    }

// ── Type-picker popup ─────────────────────────────────────────────────────────

    public class TypePickerWindow : EditorWindow {
        private IReadOnlyList<Type> _types;
        private Action<Type> _onSelected;
        private string _search = "";
        private Vector2 _scroll;

        private GUIStyle _rowStyle;
        private GUIStyle _searchStyle;
        private GUIStyle _headerStyle;

        public static void Show(IReadOnlyList<Type> types, Action<Type> onSelected) {
            var window = CreateInstance<TypePickerWindow>();
            window.titleContent = new GUIContent("  ⬡  Select Type");
            window._types = types;
            window._onSelected = onSelected;
            window.minSize = new Vector2(300, 420);
            window.maxSize = new Vector2(300, 600);
            window.ShowUtility();
        }

        private void InitStyles() {
            if (_rowStyle != null) return;

            _rowStyle = new GUIStyle(GUI.skin.button) {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                padding = new RectOffset(12, 8, 6, 6),
                margin = new RectOffset(4, 4, 1, 1),
                normal = {
                    background = MakeTex(new Color(0.22f, 0.22f, 0.23f)),
                    textColor = new Color(0.88f, 0.88f, 0.90f)
                },
                hover = {
                    background = MakeTex(new Color(0.26f, 0.26f, 0.28f)),
                    textColor = new Color(0.22f, 0.78f, 0.93f)
                },
                border = new RectOffset(4, 4, 4, 4)
            };

            _searchStyle = new GUIStyle(GUI.skin.textField) {
                fontSize = 12,
                padding = new RectOffset(10, 8, 6, 6),
                margin = new RectOffset(8, 8, 8, 6),
                normal = {
                    textColor = new Color(0.90f, 0.90f, 0.92f)
                }
            };

            _headerStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 10,
                padding = new RectOffset(10, 8, 6, 4),
                alignment = TextAnchor.MiddleLeft,
                normal = {
                    textColor = new Color(0.22f, 0.78f, 0.93f, 0.80f)
                }
            };
        }

        private void OnGUI() {
            InitStyles();

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height),
                new Color(0.17f, 0.17f, 0.18f));

            GUILayout.Space(4);
            GUILayout.Label("TYPE", _headerStyle);

            GUI.SetNextControlName("PolySearch");
            _search = EditorGUILayout.TextField(_search, _searchStyle);
            EditorGUI.FocusTextInControl("PolySearch");

            GUILayout.Space(4);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)),
                new Color(0.28f, 0.28f, 0.30f));
            GUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUIStyle.none, GUI.skin.verticalScrollbar);

            int shown = 0;
            foreach (var type in _types) {
                if (!string.IsNullOrWhiteSpace(_search) &&
                    !type.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))
                    continue;

                var rowRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rowRect, shown % 2 == 0
                    ? new Color(0.20f, 0.20f, 0.21f)
                    : new Color(0.22f, 0.22f, 0.23f));

                if (GUI.Button(rowRect, type.Name, _rowStyle)) {
                    _onSelected?.Invoke(type);
                    Close();
                }

                shown++;
            }

            if (shown == 0) {
                GUILayout.Space(12);
                var noStyle = new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleCenter, fontSize = 11,
                    normal = {
                        textColor = new Color(0.50f, 0.50f, 0.54f)
                    }
                };
                GUILayout.Label("No types found.", noStyle);
            }

            EditorGUILayout.EndScrollView();
        }

        private static Texture2D MakeTex(Color col) {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }

// ── Type cache ────────────────────────────────────────────────────────────────

    public static class PolymorphicTypeCache {
        private static readonly Dictionary<Type, List<Type>> Cache = new();

        public static IReadOnlyList<Type> GetTypes(Type baseType) {
            if (Cache.TryGetValue(baseType, out var cached)) return cached;

            var types = TypeCache
                .GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract &&
                            !t.IsGenericTypeDefinition &&
                            t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToList();

            if (!baseType.IsAbstract && !baseType.IsInterface)
                types.Insert(0, baseType);

            Cache[baseType] = types;
            return types;
        }
    }

// ── List view ─────────────────────────────────────────────────────────────────

    public class PolymorphicListView : VisualElement {
        private readonly SerializedProperty _property;
        private readonly Type _elementType;

        private int _selectedIndex = -1;
        private readonly List<bool> _collapsed = new();

        // ── Drag state ────────────────────────────────────────────────────────────
        private int _dragSourceIndex = -1; // index being dragged
        private int _dragTargetIndex = -1; // insertion slot (0...arraySize)
        private bool _isDragging;
        private VisualElement _dropIndicator; // cyan line drawn between rows

        public PolymorphicListView(SerializedProperty property, Type elementType) {
            _property = property;
            _elementType = elementType;
            Rebuild();
        }

        // ── Collapse helpers ──────────────────────────────────────────────────────

        private bool IsCollapsed(int index) {
            while (_collapsed.Count <= index) _collapsed.Add(false);
            return _collapsed[index];
        }

        private void SetCollapsed(int index, bool value) {
            while (_collapsed.Count <= index) _collapsed.Add(false);
            _collapsed[index] = value;
        }

        private void ToggleCollapsed(int index) {
            SetCollapsed(index, !IsCollapsed(index));
            Rebuild();
        }

        // ── Rebuild ───────────────────────────────────────────────────────────────

        private void Rebuild() {
            Clear();

            // ── Outer card ────────────────────────────────────────────────────────
            var card = new VisualElement {
                style = {
                    backgroundColor = PolyStyle.BgBase,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = PolyStyle.Border,
                    borderBottomColor = PolyStyle.Border,
                    borderLeftColor = PolyStyle.Border,
                    borderRightColor = PolyStyle.Border,
                    marginTop = 4,
                    marginBottom = 4,
                    overflow = Overflow.Hidden
                }
            };

            // ── Header bar ────────────────────────────────────────────────────────
            var headerBar = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = PolyStyle.BgHeader,
                    paddingLeft = 10,
                    paddingRight = 6,
                    paddingTop = 5,
                    paddingBottom = 5,
                    borderBottomWidth = 1,
                    borderBottomColor = PolyStyle.Border
                }
            };

            var stripe = new VisualElement {
                style = {
                    width = 3,
                    height = 16,
                    backgroundColor = PolyStyle.Accent,
                    borderTopLeftRadius = 2,
                    borderBottomLeftRadius = 2,
                    marginRight = 8,
                    flexShrink = 0
                }
            };
            headerBar.Add(stripe);

            var titleLabel = new Label(_property.displayName) {
                style = {
                    color = PolyStyle.TextPrimary,
                    fontSize = 12,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexGrow = 1
                }
            };
            headerBar.Add(titleLabel);

            var countLabel = new Label($"{_property.arraySize} item{(_property.arraySize == 1 ? "" : "s")}") {
                style = {
                    color = PolyStyle.TextMuted,
                    fontSize = 10,
                    marginRight = 6
                }
            };
            headerBar.Add(countLabel);

            var addBtn = PolyStyle.AccentButton("＋ Add", OpenAddMenu);
            addBtn.name = "PolyAddButton";
            headerBar.Add(addBtn);

            var removeBtn = PolyStyle.DangerButton("− Remove", RemoveSelected);
            removeBtn.name = "PolyRemoveButton";
            removeBtn.SetEnabled(_selectedIndex >= 0 && _selectedIndex < _property.arraySize);
            headerBar.Add(removeBtn);

            card.Add(headerBar);

            // ── Body ──────────────────────────────────────────────────────────────
            if (_property.arraySize == 0) {
                var empty = new Label("No entries — click  ＋ Add  to get started.") {
                    style = {
                        color = PolyStyle.TextMuted,
                        fontSize = 11,
                        paddingTop = 14,
                        paddingBottom = 14,
                        paddingLeft = 14,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                card.Add(empty);
            }
            else {
                // Reusable cyan drop-indicator line
                _dropIndicator = new VisualElement {
                    style = {
                        height = 2,
                        backgroundColor = PolyStyle.DropLine,
                        marginLeft = 4,
                        marginRight = 4,
                        display = DisplayStyle.None
                    }
                };

                for (int i = 0; i < _property.arraySize; i++)
                    card.Add(DrawElement(i, card));

                card.Add(_dropIndicator);
            }

            Add(card);
        }

        // ── Draw a single element row ─────────────────────────────────────────────

        private VisualElement DrawElement(int index, VisualElement card) {
            var element = _property.GetArrayElementAtIndex(index);
            var runtimeType = element.managedReferenceValue?.GetType();
            string typeName = runtimeType?.Name ?? "Null";
            bool isSel = _selectedIndex == index;
            bool isCollapsed = IsCollapsed(index);

            // ── Wrapper ───────────────────────────────────────────────────────────
            var wrapper = new VisualElement {
                style = {
                    backgroundColor = isSel ? PolyStyle.BgRowSel : PolyStyle.BgRow
                }
            };

            if (index > 0) {
                wrapper.style.borderTopWidth = 1;
                wrapper.style.borderTopColor = PolyStyle.Separator;
            }

            if (!isSel)
                PolyStyle.SetHover(wrapper, PolyStyle.BgRow, PolyStyle.BgRowHover);

            // ── Context menu ──────────────────────────────────────────────────────
            wrapper.AddManipulator(new ContextualMenuManipulator(evt =>
                BuildContextMenu(evt, index)));

            // ── Selection bar + content ───────────────────────────────────────────
            var rowFlex = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row
                }
            };

            var selBar = new VisualElement {
                style = {
                    width = 3,
                    flexShrink = 0,
                    backgroundColor = isSel ? PolyStyle.Accent : Color.clear
                }
            };

            var content = new VisualElement {
                style = {
                    flexGrow = 1,
                    paddingLeft = 8,
                    paddingRight = 6
                }
            };

            rowFlex.Add(selBar);
            rowFlex.Add(content);
            wrapper.Add(rowFlex);

            // ── Header row ────────────────────────────────────────────────────────
            var header = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 5,
                    paddingBottom = 5
                }
            };

            header.RegisterCallback<ClickEvent>(_ => {
                _selectedIndex = index;
                Rebuild();
            });

            // ── Drag handle ───────────────────────────────────────────────────────
            var dragHandle = new Label("⠿") {
                style = {
                    color = PolyStyle.TextMuted,
                    fontSize = 14,
                    width = 16,
                    flexShrink = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginRight = 4,
                    cursor = new StyleCursor(new Cursor())
                }
            };
            dragHandle.RegisterCallback<MouseEnterEvent>(_ => dragHandle.style.color = PolyStyle.Accent);
            dragHandle.RegisterCallback<MouseLeaveEvent>(_ => dragHandle.style.color = PolyStyle.TextMuted);

            // Mouse down on the handle starts the drag
            dragHandle.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button != 0) return;
                evt.StopPropagation();
                _dragSourceIndex = index;
                _isDragging = false; // becomes true once mouse moves
                dragHandle.CaptureMouse();
            });

            dragHandle.RegisterCallback<MouseMoveEvent>(evt => {
                if (_dragSourceIndex < 0 || !dragHandle.HasMouseCapture()) return;

                _isDragging = true;

                // Highlight the row being dragged
                wrapper.style.opacity = 0.5f;

                // Work out which slot the cursor is hovering over
                int newTarget = HitTestDropSlot(card, evt.mousePosition);
                if (newTarget != _dragTargetIndex) {
                    _dragTargetIndex = newTarget;
                    UpdateDropIndicator(card);
                }
            });

            dragHandle.RegisterCallback<MouseUpEvent>(evt => {
                if (evt.button != 0) return;
                dragHandle.ReleaseMouse();

                bool wasDragging = _isDragging;
                int src = _dragSourceIndex;
                int tgt = _dragTargetIndex;

                _dragSourceIndex = -1;
                _dragTargetIndex = -1;
                _isDragging = false;

                if (_dropIndicator != null)
                    _dropIndicator.style.display = DisplayStyle.None;

                if (wasDragging && src >= 0 && tgt >= 0 && tgt != src && tgt != src + 1)
                    MoveElement(src, tgt);
                else
                    Rebuild(); // reset opacity
            });

            header.Add(dragHandle);

            // Index badge
            header.Add(PolyStyle.IndexBadge(index));

            // Chevron collapse toggle
            var chevron = new Label(isCollapsed ? "▶" : "▼") {
                style = {
                    color = isSel ? PolyStyle.Accent : PolyStyle.TextMuted,
                    fontSize = 9,
                    width = 14,
                    flexShrink = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginRight = 4
                }
            };
            chevron.RegisterCallback<ClickEvent>(evt => {
                evt.StopPropagation();
                ToggleCollapsed(index);
            });
            chevron.RegisterCallback<MouseEnterEvent>(_ => chevron.style.color = PolyStyle.Accent);
            chevron.RegisterCallback<MouseLeaveEvent>(_ =>
                chevron.style.color = isSel ? PolyStyle.Accent : PolyStyle.TextMuted);
            header.Add(chevron);

            // Type name label
            var typeLabel = new Label(typeName) {
                style = {
                    color = isSel ? PolyStyle.Accent : PolyStyle.TextPrimary,
                    fontSize = 12,
                    unityFontStyleAndWeight = isSel ? FontStyle.Bold : FontStyle.Normal,
                    flexGrow = 1,
                    marginLeft = 2
                }
            };
            header.Add(typeLabel);

            // Action toolbar — selected row only
            if (isSel) {
                var toolbar = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center
                    }
                };
                toolbar.Add(PolyStyle.GhostButton("⇄ Type", () => OpenTypeChangeMenu(index)));
                toolbar.Add(PolyStyle.GhostButton("⧉ Dup", () => Duplicate(index)));
                header.Add(toolbar);
            }

            content.Add(header);

            // ── Child properties (any expanded row) ───────────────────────────────
            if (!isCollapsed) {
                var propsArea = new VisualElement {
                    style = {
                        paddingLeft = 8,
                        paddingRight = 6,
                        paddingBottom = 8
                    }
                };

                var rule = new VisualElement {
                    style = {
                        height = 1,
                        backgroundColor = PolyStyle.AccentDim,
                        marginBottom = 6
                    }
                };
                propsArea.Add(rule);

                DrawChildProperties(element, propsArea);
                content.Add(propsArea);
            }

            return wrapper;
        }

        // ── Context menu builder ──────────────────────────────────────────────────

        private void BuildContextMenu(ContextualMenuPopulateEvent evt, int index) {
            evt.menu.AppendAction("Change Type",
                _ => OpenTypeChangeMenu(index));

            evt.menu.AppendAction("Duplicate",
                _ => Duplicate(index));

            evt.menu.AppendSeparator();

            evt.menu.AppendAction("Move to Top",
                _ => MoveElement(index, 0),
                index == 0
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Move Up",
                _ => MoveElement(index, index - 1),
                index == 0
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Move Down",
                _ => MoveElement(index, index + 2), // +2 because target is insertion slot after next
                index == _property.arraySize - 1
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Move to Bottom",
                _ => MoveElement(index, _property.arraySize),
                index == _property.arraySize - 1
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);

            evt.menu.AppendSeparator();

            bool collapsed = IsCollapsed(index);
            evt.menu.AppendAction(collapsed ? "Expand" : "Collapse",
                _ => ToggleCollapsed(index));

            evt.menu.AppendSeparator();

            evt.menu.AppendAction("Delete",
                _ => Remove(index));
        }

        // ── Drag helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Given a mouse position in the card's local space, return which insertion
        /// slot (0 = before first, arraySize = after last) the cursor is nearest to.
        /// </summary>
        private int HitTestDropSlot(VisualElement card, Vector2 mousePos) {
            // Convert mousePos (relative to dragHandle) into card-local space
            // by walking the panel-relative positions.
            // We compare the cursor's panel-Y against each child row's midpoint.
            int best = _property.arraySize;
            bool foundCard = false;

            for (int i = 0; i < card.childCount; i++) {
                var child = card[i];
                if (child == _dropIndicator) continue;

                // card[0] is the header bar — skip it (index offset = 1)
                int rowIndex = i - 1;
                if (rowIndex < 0) continue;
                if (rowIndex >= _property.arraySize) break;

                // Get the midpoint of this row in the handle's local coord system
                var worldMid = child.LocalToWorld(new Vector2(0, child.layout.height * 0.5f));
                var handleLocal = card.WorldToLocal(worldMid);

                // mousePos here is relative to the drag handle's parent chain;
                // convert to card-local
                if (!foundCard) {
                    // On first pass establish that mouse is in card territory
                    foundCard = true;
                }

                if (mousePos.y < handleLocal.y) {
                    best = rowIndex;
                    break;
                }

                best = rowIndex + 1;
            }

            return Mathf.Clamp(best, 0, _property.arraySize);
        }

        /// <summary>
        /// Repositions the cyan drop indicator line between the appropriate rows.
        /// </summary>
        private void UpdateDropIndicator(VisualElement card) {
            if (_dropIndicator == null) return;

            // The card children are: [0] headerBar, [1...N] rows, [N+1] dropIndicator
            // We want the indicator to visually sit at slot _dragTargetIndex.
            // Simply detach and reinsert at the right position.
            if (_dropIndicator.parent != null)
                _dropIndicator.RemoveFromHierarchy();

            _dropIndicator.style.display = DisplayStyle.Flex;

            int insertAt = 1 + _dragTargetIndex; // +1 for header bar
            insertAt = Mathf.Clamp(insertAt, 1, card.childCount);
            card.Insert(insertAt, _dropIndicator);
        }

        // ── Draw serialized child fields ──────────────────────────────────────────

        private void DrawChildProperties(SerializedProperty element, VisualElement parentElement) {
            var iterator = element.Copy();
            var endProperty = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(iterator, endProperty)) {
                enterChildren = false;
                if (iterator.name == "m_Script") continue;

                var field = new PropertyField(iterator.Copy());
                field.Bind(_property.serializedObject);
                parentElement.Add(field);
            }
        }

        // ── Menu helpers ──────────────────────────────────────────────────────────

        private void OpenAddMenu() =>
            TypePickerWindow.Show(PolymorphicTypeCache.GetTypes(_elementType), AddType);

        private void OpenTypeChangeMenu(int index) =>
            TypePickerWindow.Show(
                PolymorphicTypeCache.GetTypes(_elementType),
                type => ChangeType(index, type));

        // ── Mutations ─────────────────────────────────────────────────────────────

        private void AddType(Type type) {
            Undo.RecordObject(_property.serializedObject.targetObject, "Add Polymorphic Entry");
            _property.serializedObject.Update();
            _property.arraySize++;
            _property.GetArrayElementAtIndex(_property.arraySize - 1).managedReferenceValue =
                Activator.CreateInstance(type);
            _property.serializedObject.ApplyModifiedProperties();
            _selectedIndex = _property.arraySize - 1;
            SetCollapsed(_selectedIndex, false);
            Rebuild();
        }

        private void ChangeType(int index, Type newType) {
            Undo.RecordObject(_property.serializedObject.targetObject, "Change Polymorphic Type");
            _property.serializedObject.Update();
            var element = _property.GetArrayElementAtIndex(index);
            var oldValue = element.managedReferenceValue;
            element.managedReferenceValue = PolyMigration.MigrateInto(oldValue, newType);
            _property.serializedObject.ApplyModifiedProperties();
            Rebuild();
        }

        private void RemoveSelected() {
            if (_selectedIndex >= 0 && _selectedIndex < _property.arraySize)
                Remove(_selectedIndex);
        }

        private void Remove(int index) {
            Undo.RecordObject(_property.serializedObject.targetObject, "Remove Polymorphic Entry");
            _property.serializedObject.Update();
            _property.DeleteArrayElementAtIndex(index);
            _property.serializedObject.ApplyModifiedProperties();

            if (index < _collapsed.Count)
                _collapsed.RemoveAt(index);

            _selectedIndex = _property.arraySize == 0
                ? -1
                : Mathf.Clamp(_selectedIndex, 0, _property.arraySize - 1);

            Rebuild();
        }

        private void Duplicate(int index) {
            var value = _property.GetArrayElementAtIndex(index).managedReferenceValue;
            if (value == null) return;

            var copy = Activator.CreateInstance(value.GetType());
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(value), copy);

            Undo.RecordObject(_property.serializedObject.targetObject, "Duplicate Polymorphic Entry");
            _property.serializedObject.Update();
            _property.arraySize++;
            _property.GetArrayElementAtIndex(_property.arraySize - 1).managedReferenceValue = copy;
            _property.serializedObject.ApplyModifiedProperties();

            _selectedIndex = _property.arraySize - 1;
            SetCollapsed(_selectedIndex, IsCollapsed(index));
            Rebuild();
        }

        /// <summary>
        /// Moves the element at <paramref name="srcIndex"/> so it sits at insertion
        /// slot <paramref name="dstSlot"/> (0 = before first element).
        /// Correctly handles the collapsed-state list and selection index.
        /// </summary>
        private void MoveElement(int srcIndex, int dstSlot) {
            dstSlot = Mathf.Clamp(dstSlot, 0, _property.arraySize);
            if (dstSlot == srcIndex || dstSlot == srcIndex + 1) return;

            Undo.RecordObject(_property.serializedObject.targetObject, "Reorder Polymorphic Entry");
            _property.serializedObject.Update();

            // SerializedProperty.MoveArrayElement uses a destination index (not slot)
            int dstIndex = dstSlot > srcIndex ? dstSlot - 1 : dstSlot;
            _property.MoveArrayElement(srcIndex, dstIndex);
            _property.serializedObject.ApplyModifiedProperties();

            // Mirror the move in the collapsed list
            while (_collapsed.Count <= Mathf.Max(srcIndex, dstIndex)) _collapsed.Add(false);
            bool srcCollapsed = _collapsed[srcIndex];
            _collapsed.RemoveAt(srcIndex);
            int insertAt = dstSlot > srcIndex ? dstSlot - 1 : dstSlot;
            _collapsed.Insert(insertAt, srcCollapsed);

            // Keep selection tracking the moved element
            if (_selectedIndex == srcIndex)
                _selectedIndex = insertAt;
            else if (srcIndex < _selectedIndex && dstIndex >= _selectedIndex)
                _selectedIndex--;
            else if (srcIndex > _selectedIndex && dstIndex <= _selectedIndex)
                _selectedIndex++;

            Rebuild();
        }
    } 

// ── Single Property Field View ────────────────────────────────────────────────

    public class PolymorphicFieldView : VisualElement {
        private readonly SerializedProperty _property;
        private readonly Type _baseType;
        private bool _isCollapsed;

        public PolymorphicFieldView(SerializedProperty property, Type baseType) {
            _property = property;
            _baseType = baseType;
            Rebuild();
        }

        private void Rebuild() {
            Clear();

            var runtimeType = _property.managedReferenceValue?.GetType();
            string typeName = runtimeType?.Name ?? "Null";
            bool hasValue = runtimeType != null;

            // ── Outer Card Wrapper ────────────────────────────────────────────────
            var card = new VisualElement {
                style = {
                    backgroundColor = PolyStyle.BgBase,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = PolyStyle.Border,
                    borderBottomColor = PolyStyle.Border,
                    borderLeftColor = PolyStyle.Border,
                    borderRightColor = PolyStyle.Border,
                    marginTop = 4,
                    marginBottom = 4,
                    overflow = Overflow.Hidden
                }
            };

            // ── Header Bar ────────────────────────────────────────────────────────
            var headerBar = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = PolyStyle.BgHeader,
                    paddingLeft = 10,
                    paddingRight = 6,
                    paddingTop = 5,
                    paddingBottom = 5
                }
            };

            // Left colored decorative stripe
            var stripe = new VisualElement {
                style = {
                    width = 3,
                    height = 16,
                    backgroundColor = hasValue ? PolyStyle.Accent : PolyStyle.TextMuted,
                    borderTopLeftRadius = 2,
                    borderBottomLeftRadius = 2,
                    marginRight = 8,
                    flexShrink = 0
                }
            };
            headerBar.Add(stripe);

            // Field Name Label (e.g., "My Configuration")
            var titleLabel = new Label(_property.displayName) {
                style = {
                    color = PolyStyle.TextPrimary,
                    fontSize = 12,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 8
                }
            };
            headerBar.Add(titleLabel);

            // Type string identifier context label
            var typeLabel = new Label($"({typeName})") {
                style = {
                    color = hasValue ? PolyStyle.Accent : PolyStyle.TextMuted,
                    fontSize = 11,
                    flexGrow = 1
                }
            };
            headerBar.Add(typeLabel);

            // Change / Set Type Button
            var changeBtn = PolyStyle.AccentButton(hasValue ? "⇄ Type" : "＋ Set Type", OpenTypeMenu);
            headerBar.Add(changeBtn);

            // Clear Button (Only shows if value is allocated)
            if (hasValue) {
                var clearBtn = PolyStyle.DangerButton("− Clear", ClearValue);
                headerBar.Add(clearBtn);
            }

            card.Add(headerBar);

            // ── Inner Serialized Property Fields ──────────────────────────────────
            if (hasValue) {
                // Add a foldout header divider
                headerBar.style.borderBottomWidth = 1;
                headerBar.style.borderBottomColor = PolyStyle.Border;

                var propsArea = new VisualElement {
                    style = {
                        paddingLeft = 12,
                        paddingRight = 8,
                        paddingTop = 6,
                        paddingBottom = 8,
                        backgroundColor = PolyStyle.BgRow
                    }
                };

                // Borrowing implementation from your existing drawer loop
                var iterator = _property.Copy();
                var endProperty = iterator.GetEndProperty();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren) &&
                       !SerializedProperty.EqualContents(iterator, endProperty)) {
                    enterChildren = false;
                    if (iterator.name == "m_Script") continue;

                    var field = new PropertyField(iterator.Copy());
                    field.Bind(_property.serializedObject);
                    propsArea.Add(field);
                }

                card.Add(propsArea);
            }
            else {
                // Visual feedback card when field is completely empty
                var emptyLabel = new Label("Field unassigned — click  ＋ Set Type  to initialize.") {
                    style = {
                        color = PolyStyle.TextMuted,
                        fontSize = 11,
                        paddingTop = 10,
                        paddingBottom = 10,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                card.Add(emptyLabel);
            }

            Add(card);
        }

        private void OpenTypeMenu() =>
            TypePickerWindow.Show(PolymorphicTypeCache.GetTypes(_baseType), AssignType);

        private void AssignType(Type type) {
            Undo.RecordObject(_property.serializedObject.targetObject, "Assign Polymorphic Field Type");
            _property.serializedObject.Update();

            var oldValue = _property.managedReferenceValue;
            // Safely migrates field values across if changing types instead of instantiating fresh!
            _property.managedReferenceValue = PolyMigration.MigrateInto(oldValue, type);

            _property.serializedObject.ApplyModifiedProperties();
            Rebuild();
        }

        private void ClearValue() {
            Undo.RecordObject(_property.serializedObject.targetObject, "Clear Polymorphic Field");
            _property.serializedObject.Update();
            _property.managedReferenceValue = null;
            _property.serializedObject.ApplyModifiedProperties();
            Rebuild();
        }
    }

// ── Custom editor ─────────────────────────────────────────────────────────────

    [CustomEditor(typeof(MonoBehaviour), true)]
    public class PolymorphicEditor : UnityEditor.Editor {
        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new();

        private static FieldInfo[] GetFields(Type type) {
            if (FieldCache.TryGetValue(type, out var cached)) return cached;
            var fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.FlattenHierarchy);
            FieldCache[type] = fields;
            return fields;
        }

        public override VisualElement CreateInspectorGUI() {
            var root = new VisualElement {
                style = {
                    paddingLeft = 2,
                    paddingRight = 2
                }
            };

            foreach (var field in GetFields(target.GetType())) {
                var property = serializedObject.FindProperty(field.Name);
                if (property == null) continue;

                bool isGnomeable = field.GetCustomAttribute<GnomeableAttribute>() != null;

                if (!isGnomeable) {
                    root.Add(new PropertyField(property));
                    continue;
                }

                // Check if the type is a generic container (e.g., List<T>)
                if (field.FieldType.IsGenericType) {
                    var genericArgs = field.FieldType.GetGenericArguments();
                    if (genericArgs.Length != 1) {
                        root.Add(new HelpBox(
                            $"[Gnomeable] on '{field.Name}' requires a single-generic-argument type (e.g. List<T>).",
                            HelpBoxMessageType.Error));
                        continue;
                    }

                    root.Add(new PolymorphicListView(property, genericArgs[0]));
                }
                else {
                    // It's a single property field!
                    root.Add(new PolymorphicFieldView(property, field.FieldType));
                }
            }

            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(ScriptableObject), true)]
    public class PolymorphicSOEditor : PolymorphicEditor {
    }

// ── Gnomeable validator ───────────────────────────────────────────────────────
// Runs once on domain reload and warns if any [Gnomeable] field is missing
// [SerializeReference], which Unity requires for polymorphic serialization.

    [InitializeOnLoad]
    internal static class GnomeableValidator {
        static GnomeableValidator() {
            foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(MonoBehaviour)))
                ValidateType(type);
        }

        private static void ValidateType(Type type) {
            foreach (var field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.Public |
                         BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
                if (field.GetCustomAttribute<GnomeableAttribute>() == null) continue;
                if (field.GetCustomAttribute<SerializeReference>() != null) continue;

                Debug.LogWarning(
                    $"[Gnomeable] <b>{type.Name}.{field.Name}</b> is missing " +
                    $"<b>[SerializeReference]</b>. Polymorphic serialization will not work without it.\n" +
                    $"Fix: add [SerializeReference] above [Gnomeable] on this field.");
            }
        }
    }
}