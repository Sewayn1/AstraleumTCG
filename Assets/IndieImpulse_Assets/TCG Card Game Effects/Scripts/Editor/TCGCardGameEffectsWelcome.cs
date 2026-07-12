using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
namespace IndieImpulseAssets
{
    public class TCGCardGameEffectsWelcome : EditorWindow
    {
        // ── State ────────────────────────────────────────────────────────────────
        private enum Pipeline { None, BuiltIn, URP, HDRP }
        private Pipeline selectedPipeline = Pipeline.None;

        private Pipeline _detectedPipeline = Pipeline.None;

        private static readonly System.Collections.Generic.HashSet<Pipeline> SupportedPipelines =
    new System.Collections.Generic.HashSet<Pipeline>
    {
        Pipeline.BuiltIn,
        Pipeline.URP,
    };

        private bool builtInPackageImported = false;
        private bool shaderGraphInstalled = false;
        private bool postProcessInstalled = false;
        private bool hdrpPackageImported = false;

        private AddRequest packageRequest;
        private bool isInstallingPackage = false;
        private string installingLabel = "";

        // FIX #6 – store the actual delegate so we can unsubscribe it later
        private EditorApplication.CallbackFunction pollDelegate;

        // Package IDs
        private const string SHADER_GRAPH_ID = "com.unity.shadergraph";
        private const string POST_PROCESS_ID = "com.unity.postprocessing";
        private const string HDRP_ID = "com.unity.render-pipelines.high-definition";

        // Contact info
        private const string WEBSITE_URL = "https://indieimpulse.com/";
        private const string DISCORD_URL = "https://discord.com/invite/SR2gH9ZmWu";
        private const string SUPPORT_EMAIL = "support@indieimpulse.com";

        // Pipeline help texture path
        private const string PIPELINE_HELP_TEXTURE =
            "Assets/IndieImpulse_Assets/TCG Card Game Effects/Textures/PipelineHelp.png";

        // "Do not show again" preference
        private bool dontShowAgain;
        private const string PREFS_DONT_SHOW_AGAIN = "UEMVFX_DontShowAgain";

        // ── Styling ──────────────────────────────────────────────────────────────
        private GUIStyle styleTitle;
        private GUIStyle styleSubtitle;
        private GUIStyle styleSectionHeader;
        private GUIStyle styleBody;
        private GUIStyle styleLinkButton;

        // FIX #1 – keep references so we can destroy them on disable
        private Texture2D texAccent;
        private Texture2D texDark;
        private Texture2D texCard;
        private Texture2D texGreen;
        private Texture2D texButtonBlue;
        private Texture2D texButtonHover;
        private Texture2D texPipelineSelected;
        private Texture2D texPipelineNormal;

        // FIX #2 – separate flag so textures can be re-created after domain reload
        private bool stylesInitialised = false;
        private bool texturesInitialised = false;
        private Vector2 scroll;

        // Pipeline icon colours
        private static readonly Color COL_BUILTIN = new Color(0.35f, 0.65f, 1.00f);
        private static readonly Color COL_URP = new Color(0.40f, 0.85f, 0.60f);
        private static readonly Color COL_HDRP = new Color(0.85f, 0.50f, 1.00f);
        private static readonly Color COL_ACCENT = new Color(0.98f, 0.72f, 0.18f);
        private static readonly Color COL_BG = new Color(0.13f, 0.13f, 0.16f);
        private static readonly Color COL_CARD = new Color(0.18f, 0.18f, 0.22f);
        private static readonly Color COL_SUCCESS = new Color(0.25f, 0.78f, 0.45f);

        // ── Entry point ──────────────────────────────────────────────────────────
        [MenuItem("Tools/TCG Card Game Effects/Welcome & Setup")]
        public static void ShowWindow()
        {
            var win = GetWindow<TCGCardGameEffectsWelcome>(true, "TCG Card Game Effects – Setup", true);
            win.minSize = new Vector2(540, 700);
            win.maxSize = new Vector2(540, 900);
        }

        [InitializeOnLoadMethod]
        private static void AutoOpen()
        {
            if (EditorUserSettings.GetConfigValue(PREFS_DONT_SHOW_AGAIN) == "true")
                return;

            // FIX #3 – guard against repeated opens across domain reloads by combining
            // SessionState (per-session) with a per-launch EditorPrefs key.
            if (SessionState.GetBool("UEMVFX_Opened", false))
                return;

            SessionState.SetBool("UEMVFX_Opened", true);
            EditorApplication.delayCall += ShowWindow;
        }

        private void OnEnable()
        {
            _detectedPipeline = DetectPipeline();
            selectedPipeline = SupportedPipelines.Contains(_detectedPipeline)
    ? _detectedPipeline
    : Pipeline.None;
            dontShowAgain = EditorUserSettings.GetConfigValue(PREFS_DONT_SHOW_AGAIN) == "true";

            // FIX #2 – always reset texture flag on enable so textures are rebuilt
            // after a domain reload (Unity destroys unmanaged textures across reloads).
            texturesInitialised = false;
            stylesInitialised = false;
        }

        private void OnDisable()
        {
            // Save preference
            EditorUserSettings.SetConfigValue(PREFS_DONT_SHOW_AGAIN, dontShowAgain ? "true" : "false");

            // FIX #1 – destroy all dynamically created textures to prevent memory leaks
            DestroyTexture(ref texAccent);
            DestroyTexture(ref texDark);
            DestroyTexture(ref texCard);
            DestroyTexture(ref texGreen);
            DestroyTexture(ref texButtonBlue);
            DestroyTexture(ref texButtonHover);
            DestroyTexture(ref texPipelineSelected);
            DestroyTexture(ref texPipelineNormal);

            texturesInitialised = false;
            stylesInitialised = false;

            // FIX #6 – unsubscribe any lingering poll delegate
            if (pollDelegate != null)
            {
                EditorApplication.update -= pollDelegate;
                pollDelegate = null;
            }
        }

        // FIX #1 – helper to safely destroy a texture and null the reference
        private static void DestroyTexture(ref Texture2D tex)
        {
            if (tex != null)
            {
                DestroyImmediate(tex);
                tex = null;
            }
        }

        // ── Pipeline detection ───────────────────────────────────────────────────
        private Pipeline DetectPipeline()
        {
#if UNITY_6000_0_OR_NEWER
            var pipelineAsset = GraphicsSettings.defaultRenderPipeline;
#else
            var pipelineAsset = GraphicsSettings.renderPipelineAsset;
#endif

            if (pipelineAsset == null)
                return Pipeline.BuiltIn;

            string fullName = pipelineAsset.GetType().FullName ?? "";

            if (fullName.Contains("UniversalRenderPipeline") ||
                fullName.Contains("Universal.UniversalRenderPipelineAsset"))
                return Pipeline.URP;

            if (fullName.Contains("HighDefinition") ||
                fullName.Contains("HDRenderPipeline"))
                return Pipeline.HDRP;

            return Pipeline.BuiltIn;
        }


        // ── Texture helpers ──────────────────────────────────────────────────────
        private Texture2D MakeTex(int w, int h, Color col)
        {
            var t = new Texture2D(w, h);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            t.SetPixels(pixels);
            t.Apply();
            return t;
        }

        // FIX #2 – split texture init from style init so textures can be rebuilt
        private void InitTextures()
        {
            if (texturesInitialised) return;

            // Guard: if any texture is null (e.g. after domain reload) rebuild all
            if (texAccent != null) return;

            texturesInitialised = true;

            texAccent = MakeTex(1, 1, COL_ACCENT);
            texDark = MakeTex(1, 1, COL_BG);
            texCard = MakeTex(1, 1, COL_CARD);
            texGreen = MakeTex(1, 1, COL_SUCCESS);
            texButtonBlue = MakeTex(1, 1, new Color(0.20f, 0.50f, 0.95f));
            texButtonHover = MakeTex(1, 1, new Color(0.30f, 0.60f, 1.00f));
            texPipelineSelected = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.30f));
            texPipelineNormal = MakeTex(1, 1, new Color(0.20f, 0.20f, 0.24f));
        }

        private void InitStyles()
        {
            // FIX #2 – always rebuild styles when textures change
            if (stylesInitialised && texAccent != null) return;

            InitTextures();
            stylesInitialised = true;

            styleTitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            styleSubtitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.75f, 0.75f, 0.80f) }
            };

            styleSectionHeader = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = COL_ACCENT }
            };

            styleBody = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.80f, 0.80f, 0.85f) }
            };

            // FIX #10 – add hover state to link button so it doesn't go grey
            styleLinkButton = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.40f, 0.75f, 1.00f) },
                hover = { textColor = Color.white },
                active = { textColor = new Color(0.60f, 0.90f, 1.00f) }
            };
        }

        // ── Package manager helpers ──────────────────────────────────────────────
        private void InstallPackage(string packageId, string label, System.Action onDone)
        {
            if (isInstallingPackage) return;

            isInstallingPackage = true;
            installingLabel = label;
            packageRequest = Client.Add(packageId);

            // FIX #6 – store delegate reference so we can unsubscribe it correctly
            pollDelegate = () => PollRequest(onDone);
            EditorApplication.update += pollDelegate;
        }

        private void PollRequest(System.Action onDone)
        {
            if (packageRequest == null || !packageRequest.IsCompleted) return;

            // FIX #6 – unsubscribe using the stored reference, not a new lambda
            EditorApplication.update -= pollDelegate;
            pollDelegate = null;

            isInstallingPackage = false;
            installingLabel = "";

            // FIX #5 – handle both success and failure paths cleanly
            if (packageRequest.Status == StatusCode.Success)
            {
                onDone?.Invoke();
            }
            else
            {
                Debug.LogError("[TCG Card Game Effects] Package install failed: " +
                               packageRequest.Error?.message);
            }

            packageRequest = null;
            Repaint();
        }

        // ── GUI ──────────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            InitStyles();

            if (texDark != null)
                GUI.DrawTexture(new Rect(0, 0, position.width, position.height), texDark, ScaleMode.StretchToFill);
            else
                EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), COL_BG);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUIStyle.none, GUIStyle.none);

            DrawHeader();
            GUILayout.Space(12);
            DrawDivider();
            GUILayout.Space(12);
            DrawPipelineSection();
            GUILayout.Space(12);
            DrawDivider();
            GUILayout.Space(12);
            DrawSetupSteps();
            GUILayout.Space(12);
            DrawDivider();
            GUILayout.Space(12);
            DrawSupportSection();
            GUILayout.Space(20);

            EditorGUILayout.EndScrollView();
        }

        // ── Header ───────────────────────────────────────────────────────────────
        private void DrawHeader()
        {
            var accentRect = GUILayoutUtility.GetRect(position.width, 5);
            if (texAccent != null)
                GUI.DrawTexture(accentRect, texAccent, ScaleMode.StretchToFill);
            else
                EditorGUI.DrawRect(accentRect, COL_ACCENT);

            GUILayout.Space(20);
            GUILayout.Label("⚡  TCG Card Game Effects", styleTitle);
            GUILayout.Space(6);
            GUILayout.Label("Welcome! Thank you for choosing this asset.\nLet's get you set up in just a few steps.", styleSubtitle);
            GUILayout.Space(10);
        }

        // ── Divider ──────────────────────────────────────────────────────────────
        private void DrawDivider()
        {
            var r = GUILayoutUtility.GetRect(position.width, 1);
            EditorGUI.DrawRect(r, new Color(1, 1, 1, 0.08f));
        }

        // ── Pipeline selection ───────────────────────────────────────────────────
        private void DrawPipelineSection()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label("Step 1 — Select Your Render Pipeline", styleSectionHeader);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Space(16);

            var helpButtonStyle = new GUIStyle(styleBody)
            {
                normal = { textColor = new Color(0.40f, 0.75f, 1.00f) },
                hover = { textColor = Color.white },
                active = { textColor = new Color(0.60f, 0.90f, 1.00f) },
                alignment = TextAnchor.MiddleLeft
            };

            if (GUILayout.Button("❓ Not sure which pipeline you are using? Click for help.", helpButtonStyle))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PIPELINE_HELP_TEXTURE);
                if (texture != null)
                {
                    EditorGUIUtility.PingObject(texture);
                    AssetDatabase.OpenAsset(texture);
                }
                else
                {
                    Debug.LogWarning($"[TCG Card Game Effects] Pipeline help texture not found at: {PIPELINE_HELP_TEXTURE}");
                }
            }

            GUILayout.Space(16);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            if (SupportedPipelines.Contains(Pipeline.BuiltIn)) { DrawPipelineButton("Built-In", Pipeline.BuiltIn, COL_BUILTIN); GUILayout.Space(8); }
            if (SupportedPipelines.Contains(Pipeline.URP)) { DrawPipelineButton("URP", Pipeline.URP, COL_URP); GUILayout.Space(8); }
            if (SupportedPipelines.Contains(Pipeline.HDRP)) { DrawPipelineButton("HDRP", Pipeline.HDRP, COL_HDRP); GUILayout.Space(8); }
            GUILayout.Space(16);
            GUILayout.EndHorizontal();
        }

        private void DrawPipelineButton(string label, Pipeline pipe, Color accent)
        {
            bool isSelected = (selectedPipeline == pipe);
            bool isLight = !EditorGUIUtility.isProSkin;

            // Theme-aware colors
            Color bgNormal = isLight ? new Color(0.82f, 0.82f, 0.85f) : new Color(0.20f, 0.20f, 0.24f);
            Color bgSelected = isLight ? new Color(0.72f, 0.72f, 0.78f) : new Color(0.25f, 0.25f, 0.30f);
            Color textNormal = isLight ? new Color(0.25f, 0.25f, 0.30f) : new Color(0.70f, 0.70f, 0.75f);
            Color borderColor = isLight ? accent * 0.85f : accent;
            Color canvasBg = isLight ? new Color(0.75f, 0.75f, 0.80f) : accent;

            Texture2D bgNormalTex = isLight ? MakeTex(1, 1, bgNormal) : texPipelineNormal;
            Texture2D bgSelectedTex = isLight ? MakeTex(1, 1, bgSelected) : texPipelineSelected;

            var bg = isSelected ? bgSelectedTex : bgNormalTex;

            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = bg, textColor = isSelected ? (isLight ? accent * 0.75f : accent) : textNormal },
                hover = { background = bgSelectedTex, textColor = isLight ? accent * 0.75f : accent },
                active = { background = bg, textColor = isLight ? accent * 0.75f : accent },
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(0, 0, 10, 10)
            };

            float w = (position.width - 32 - 16) / 3f;

            if (isSelected)
            {
                var canvasRect = GUILayoutUtility.GetRect(w, 44);
                EditorGUI.DrawRect(
                    new Rect(canvasRect.x - 2, canvasRect.y - 2, canvasRect.width + 4, canvasRect.height + 4),
                    isLight ? borderColor : accent
                );
                if (GUI.Button(canvasRect, label, style))
                {
                    if (pipe != _detectedPipeline)
                    {
                        string detectedName = _detectedPipeline == Pipeline.BuiltIn ? "Built-In" :
                                             _detectedPipeline == Pipeline.URP ? "URP" : "HDRP";
                        bool confirm = EditorUtility.DisplayDialog(
                            "Switch Render Pipeline?",
                            $"This project was detected as {detectedName}. Are you sure you want to switch to {label}?",
                            "Yes, Switch",
                            "Cancel"
                        );
                        if (!confirm) return;
                    }
                    selectedPipeline = pipe;
                    builtInPackageImported = false;
                    shaderGraphInstalled = false;
                    postProcessInstalled = false;
                    hdrpPackageImported = false;
                    Repaint();
                }

                DrawArrowBelow(canvasRect, accent);
            }
            else
            {
                // YENİ
                if (GUILayout.Button(label, style, GUILayout.Width(w), GUILayout.Height(44)))
                {
                    if (pipe != _detectedPipeline)
                    {
                        string detectedName = _detectedPipeline == Pipeline.BuiltIn ? "Built-In" :
                                             _detectedPipeline == Pipeline.URP ? "URP" : "HDRP";
                        bool confirm = EditorUtility.DisplayDialog(
                            "Switch Render Pipeline?",
                            $"This project was detected as {detectedName}. Are you sure you want to switch to {label}?",
                            "Yes, Switch",
                            "Cancel"
                        );
                        if (!confirm) return;
                    }
                    selectedPipeline = pipe;
                    builtInPackageImported = false;
                    shaderGraphInstalled = false;
                    postProcessInstalled = false;
                    hdrpPackageImported = false;
                    Repaint();
                }
            }

            if (isLight)
            {
                DestroyImmediate(bgNormalTex);
                DestroyImmediate(bgSelectedTex);
            }
        }

        private void DrawArrowBelow(Rect buttonRect, Color color)
        {
            float arrowSize = 16f;
            float arrowY = buttonRect.y + buttonRect.height + 2;
            float arrowX = buttonRect.x + (buttonRect.width - arrowSize) / 2;

            var arrowStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 20,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = color }
            };
            GUI.Label(new Rect(arrowX, arrowY, arrowSize, arrowSize), "▲", arrowStyle);
        }

        // ── Setup steps ──────────────────────────────────────────────────────────
        private void DrawSetupSteps()
        {
            if (selectedPipeline == Pipeline.None) return;

            if (selectedPipeline == Pipeline.URP)
            {
                DrawURPReadyMessage();
                return;
            }

            // Detected banner for Built-In and HDRP
            string pipeName = selectedPipeline == Pipeline.BuiltIn ? "Built-In" : "HDRP";
            Color pipeColor = selectedPipeline == Pipeline.BuiltIn ? COL_BUILTIN : COL_HDRP;
            Texture2D bannerTex = MakeTex(1, 1, new Color(pipeColor.r * 0.25f, pipeColor.g * 0.25f, pipeColor.b * 0.25f));
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            var bannerStyle = new GUIStyle
            {
                normal = { background = bannerTex },
                padding = new RectOffset(14, 14, 10, 10)
            };
            GUILayout.BeginVertical(bannerStyle, GUILayout.Width(position.width - 32));
            var bannerTextStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = pipeColor }
            };
            GUILayout.Label($"◈  {pipeName} Detected – Complete the steps below to finish setup.", bannerTextStyle);
            GUILayout.EndVertical();
            GUILayout.Space(16);
            GUILayout.EndHorizontal();
            DestroyImmediate(bannerTex);

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label("Step 2 — Install Required Packages", styleSectionHeader);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            switch (selectedPipeline)
            {
                case Pipeline.BuiltIn: DrawBuiltInSteps(); break;
                case Pipeline.HDRP: DrawHDRPSteps(); break;
            }

            GUILayout.Space(8);
            DrawReadinessStatus();
        }

        // ·· URP ready message ···················
        private void DrawURPReadyMessage()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);

            var cardStyle = new GUIStyle
            {
                normal = { background = texGreen },
                padding = new RectOffset(20, 20, 16, 16)
            };
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(position.width - 32));

            var whiteTextStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            GUILayout.Label("✔  URP Detected – Everything is Ready!", whiteTextStyle);
            GUILayout.Space(6);

            var whiteBodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            GUILayout.Label("No additional packages or imports are required.", whiteBodyStyle);

            GUILayout.Space(12);
            if (GUILayout.Button("Open Demo Scene", GUILayout.Height(30)))
                TryOpenDemoScene();

            GUILayout.EndVertical();
            GUILayout.Space(16);
            GUILayout.EndHorizontal();
        }

        // ·· Built-In ·············
        private void DrawBuiltInSteps()
        {
            DrawStepCard(
                "1. Import Built-In Package",
                "Import the Built-In render pipeline support package included with this asset.",
                builtInPackageImported,
                !builtInPackageImported,
                "Import Built-In Package",
                () =>
                {
                    // FIX #7 – verify the package exists before attempting import
                    string packagePath = "Assets/IndieImpulse_Assets/TCG Card Game Effects/Built-in.unitypackage";
                    if (!System.IO.File.Exists(packagePath))
                    {
                        Debug.LogError($"[TCG Card Game Effects] Package not found at: {packagePath}");
                        return;
                    }
                    AssetDatabase.ImportPackage(packagePath, true);
                    builtInPackageImported = true;
                }
            );

            GUILayout.Space(6);

            DrawStepCard(
                "2. Install Shader Graph (latest)",
                "Shader Graph is required for the advanced material effects. Install it from the Unity Registry.",
                shaderGraphInstalled,
                builtInPackageImported && !shaderGraphInstalled,
                isInstallingPackage && installingLabel == "Shader Graph"
                    ? "Installing… Please wait."
                    : "Install via Unity Registry",
                () => InstallPackage(SHADER_GRAPH_ID, "Shader Graph", () => shaderGraphInstalled = true)
            );

            GUILayout.Space(6);

            DrawStepCard(
                "3. Install Post Processing",
                "Post Processing adds the visual polish for bloom, colour grading and more.",
                postProcessInstalled,
                shaderGraphInstalled && !postProcessInstalled,
                isInstallingPackage && installingLabel == "Post Processing"
                    ? "Installing… Please wait."
                    : "Install via Unity Registry",
                () => InstallPackage(POST_PROCESS_ID, "Post Processing", () => postProcessInstalled = true)
            );
        }

        // ·· HDRP ·················
        private void DrawHDRPSteps()
        {
            DrawStepCard(
                "1. Import HDRP Package",
                "Import the HDRP support package included with this asset to activate all HDRP-compatible materials and effects.",
                hdrpPackageImported,
                !hdrpPackageImported,
                "Import HDRP Package",
                () =>
                {
                    // FIX #7 – verify the package exists before attempting import
                    string packagePath = "Assets/IndieImpulse_Assets/TCG Card Game Effects/HDRP.unitypackage";
                    if (!System.IO.File.Exists(packagePath))
                    {
                        Debug.LogError($"[TCG Card Game Effects] Package not found at: {packagePath}");
                        return;
                    }
                    AssetDatabase.ImportPackage(packagePath, true);
                    hdrpPackageImported = true;
                }
            );
        }

        // ·· Shared step card ·····
        private void DrawStepCard(string title, string description, bool done, bool actionEnabled,
                                   string buttonLabel, System.Action onAction)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);

            var cardStyle = new GUIStyle
            {
                normal = { background = texCard },
                padding = new RectOffset(14, 14, 12, 12)
            };
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(position.width - 32));

            GUILayout.BeginHorizontal();
            var titleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = done ? COL_SUCCESS : Color.white }
            };
            GUILayout.Label((done ? "✔  " : "○  ") + title, titleStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label(description, styleBody);
            GUILayout.Space(8);

            // FIX #4 – wrap GUI.enabled in try/finally so it is always restored
            if (!done)
            {
                bool wasEnabled = GUI.enabled;
                try
                {
                    GUI.enabled = actionEnabled && !isInstallingPackage;

                    var btnStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                        normal = { background = texButtonBlue, textColor = Color.white },
                        hover = { background = texButtonHover, textColor = Color.white },
                        active = { background = texButtonBlue, textColor = Color.white },
                        padding = new RectOffset(12, 12, 7, 7)
                    };

                    if (GUILayout.Button(buttonLabel, btnStyle, GUILayout.Width(200)))
                        onAction?.Invoke();
                }
                finally
                {
                    // FIX #4 – always restore previous GUI.enabled state
                    GUI.enabled = wasEnabled;
                }
            }

            GUILayout.EndVertical();
            GUILayout.Space(16);
            GUILayout.EndHorizontal();
        }

        // ·· Readiness status ·····
        private bool IsReady()
        {
            switch (selectedPipeline)
            {
                case Pipeline.URP: return true;
                case Pipeline.BuiltIn: return builtInPackageImported && shaderGraphInstalled && postProcessInstalled;
                case Pipeline.HDRP: return hdrpPackageImported;
                default: return false;
            }
        }

        private void DrawReadinessStatus()
        {
            if (selectedPipeline == Pipeline.None) return;

            bool ready = IsReady();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);

            var bg = ready ? texGreen : texAccent;
            var statusStyle = new GUIStyle
            {
                normal = { background = bg },
                padding = new RectOffset(12, 12, 8, 8)
            };
            var textStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUILayout.BeginVertical(statusStyle, GUILayout.Width(position.width - 32));
            GUILayout.Label(
                ready ? "🎉 Everything is set up! Your asset is ready to use."
                      : "⏳ Complete the steps above to finish setup.",
                textStyle
            );

            if (ready)
            {
                GUILayout.Space(8);
                if (GUILayout.Button("Open Demo Scene", GUILayout.Height(30)))
                    TryOpenDemoScene();
            }

            GUILayout.EndVertical();
            GUILayout.Space(16);
            GUILayout.EndHorizontal();
        }

        // ── Shared demo scene opener ─────────────────────────────────────────────
        private static void TryOpenDemoScene()
        {
            const string scenePath =
                "Assets/IndieImpulse_Assets/TCG Card Game Effects/Scenes/TCG Card Game EffectsDEMO.unity";

            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogError($"[TCG Card Game Effects] Demo scene not found at: {scenePath}");
                return;
            }

            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            GetWindow<TCGCardGameEffectsWelcome>().Close();
        }

        // ── Support section ──────────────────────────────────────────────────────
        private void DrawSupportSection()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label("Need Help?", styleSectionHeader);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label(
                "If you encounter any issues or have custom requests for your project, feel free to reach out. " +
                "You can join our Discord community, submit a support form on our website, or send us an email.",
                styleBody,
                GUILayout.Width(position.width - 32)
            );
            GUILayout.Space(16);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            DrawContactRow("Website & Support Form", WEBSITE_URL, WEBSITE_URL);
            GUILayout.Space(4);
            DrawContactRow("Discord Community", DISCORD_URL, DISCORD_URL);
            GUILayout.Space(4);
            DrawContactRow("Email Support", SUPPORT_EMAIL, "mailto:" + SUPPORT_EMAIL);

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);

            var toggleStyle = new GUIStyle(EditorStyles.toggle)
            {
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                onNormal = { textColor = Color.white },
                onHover = { textColor = Color.white }
            };

            var toggleIcon = EditorGUIUtility.IconContent("Toggle").image as Texture2D;
            var toggleOnIcon = EditorGUIUtility.IconContent("Toggle On").image as Texture2D;

            if (toggleIcon != null) toggleStyle.normal.background = toggleStyle.hover.background = toggleIcon;
            if (toggleOnIcon != null) toggleStyle.onNormal.background = toggleStyle.onHover.background = toggleOnIcon;

            bool newDontShow = GUILayout.Toggle(dontShowAgain,
                "Do not show this window automatically again", toggleStyle);

            if (newDontShow != dontShowAgain)
            {
                dontShowAgain = newDontShow;
                EditorUserSettings.SetConfigValue(PREFS_DONT_SHOW_AGAIN, dontShowAgain ? "true" : "false");
            }

            GUILayout.Space(16);
            GUILayout.EndHorizontal();
        }

        private void DrawContactRow(string label, string display, string url)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);

            var rowStyle = new GUIStyle
            {
                normal = { background = texCard },
                padding = new RectOffset(12, 12, 8, 8)
            };

            GUILayout.BeginHorizontal(rowStyle, GUILayout.Width(position.width - 32));
            GUILayout.Label(label, styleBody, GUILayout.Width(190));

            if (GUILayout.Button(display, styleLinkButton))
                Application.OpenURL(url);

            GUILayout.EndHorizontal();
            GUILayout.Space(16);
            GUILayout.EndHorizontal();
        }
    }
}