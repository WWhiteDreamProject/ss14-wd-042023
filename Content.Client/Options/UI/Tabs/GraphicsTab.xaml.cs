using Content.Client.UserInterface.Screens;
using Content.Shared.CCVar;
using Content.Shared.HUD;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.Options.UI.Tabs
{
    [GenerateTypedNameReferences]
    public sealed partial class GraphicsTab : Control
    {
        private static readonly float[] UIScaleOptions =
        {
            0f,
            0.75f,
            1f,
            1.25f,
            1.50f,
            1.75f,
            2f
        };

        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        private readonly Dictionary<ScreenType, string> _screenTypeToLoc = new()
        {
            {ScreenType.Default, Loc.GetString("ui-options-hud-theme-modernized")},
            {ScreenType.Separated, Loc.GetString("ui-options-hud-theme-classic")},
        };

        public GraphicsTab()
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);

            VSyncCheckBox.OnToggled += OnCheckBoxToggled;
            FullscreenCheckBox.OnToggled += OnCheckBoxToggled;

            LightingPresetOption.AddItem(Loc.GetString("ui-options-lighting-very-low"));
            LightingPresetOption.AddItem(Loc.GetString("ui-options-lighting-low"));
            LightingPresetOption.AddItem(Loc.GetString("ui-options-lighting-medium"));
            LightingPresetOption.AddItem(Loc.GetString("ui-options-lighting-high"));
            LightingPresetOption.OnItemSelected += OnLightingQualityChanged;

            UIScaleOption.AddItem(Loc.GetString("ui-options-scale-auto",
                                                ("scale", UserInterfaceManager.DefaultUIScale)));
            UIScaleOption.AddItem(Loc.GetString("ui-options-scale-75"));
            UIScaleOption.AddItem(Loc.GetString("ui-options-scale-100"));
            UIScaleOption.AddItem(Loc.GetString("ui-options-scale-125"));
            UIScaleOption.AddItem(Loc.GetString("ui-options-scale-150"));
            UIScaleOption.AddItem(Loc.GetString("ui-options-scale-175"));
            UIScaleOption.AddItem(Loc.GetString("ui-options-scale-200"));
            UIScaleOption.OnItemSelected += OnUIScaleChanged;

            foreach (var gear in _prototypeManager.EnumeratePrototypes<HudThemePrototype>())
            {
                HudThemeOption.AddItem(Loc.GetString(gear.Name));
            }
            HudThemeOption.OnItemSelected += OnHudThemeChanged;

            var hudLayout = _cfg.GetCVar(CCVars.UILayout);
            var id = 0;
            foreach (var layout in Enum.GetValues(typeof(ScreenType)))
            {
                var name = layout.ToString()!;

                var buttonText = _screenTypeToLoc[(ScreenType) layout];
                HudLayoutOption.AddItem(buttonText, id);

                if (name == hudLayout)
                {
                    HudLayoutOption.SelectId(id);
                    HudLayoutWarningText.Visible = name == ScreenType.Default.ToString();
                }
                HudLayoutOption.SetItemMetadata(id, name);

                id++;
            }

            HudLayoutOption.OnItemSelected += args =>
            {
                HudLayoutOption.SelectId(args.Id);

                var metaData = (string)HudLayoutOption.GetItemMetadata(args.Id)!;

                HudLayoutWarningText.Visible = metaData == ScreenType.Default.ToString();

                UpdateApplyButton();
            };

            ViewportStretchCheckBox.OnToggled += _ =>
            {
                UpdateViewportScale();
                UpdateApplyButton();
            };

            ViewportScaleSlider.OnValueChanged += _ =>
            {
                UpdateApplyButton();
                UpdateViewportScale();
            };

            ViewportWidthSlider.OnValueChanged += _ =>
            {
                UpdateViewportWidthDisplay();
                UpdateApplyButton();
            };

            ShowHeldItemCheckBox.OnToggled += OnCheckBoxToggled;
            IntegerScalingCheckBox.OnToggled += OnCheckBoxToggled;
            ViewportLowResCheckBox.OnToggled += OnCheckBoxToggled;
            ParallaxLowQualityCheckBox.OnToggled += OnCheckBoxToggled;
            FpsCounterCheckBox.OnToggled += OnCheckBoxToggled;
            FilmGrainCheckBox.OnToggled += OnFilmGrainCheckBoxToggled;
            ApplyButton.OnPressed += OnApplyButtonPressed;
            VSyncCheckBox.Pressed = _cfg.GetCVar(CVars.DisplayVSync);
            FullscreenCheckBox.Pressed = ConfigIsFullscreen;
            LightingPresetOption.SelectId(GetConfigLightingQuality());
            UIScaleOption.SelectId(GetConfigUIScalePreset(ConfigUIScale));
            HudThemeOption.SelectId(_cfg.GetCVar(CCVars.HudTheme));
            ViewportScaleSlider.Value = _cfg.GetCVar(CCVars.ViewportFixedScaleFactor);
            ViewportStretchCheckBox.Pressed = _cfg.GetCVar(CCVars.ViewportStretch);
            IntegerScalingCheckBox.Pressed = _cfg.GetCVar(CCVars.ViewportSnapToleranceMargin) != 0;
            ViewportLowResCheckBox.Pressed = !_cfg.GetCVar(CCVars.ViewportScaleRender);
            ParallaxLowQualityCheckBox.Pressed = _cfg.GetCVar(CCVars.ParallaxLowQuality);
            FpsCounterCheckBox.Pressed = _cfg.GetCVar(CCVars.HudFpsCounterVisible);
            ShowHeldItemCheckBox.Pressed = _cfg.GetCVar(CCVars.HudHeldItemShow);
            ViewportWidthSlider.Value = _cfg.GetCVar(CCVars.ViewportWidth);
            FilmGrainCheckBox.Pressed = _cfg.GetCVar(CCVars.FilmGrain);

            _cfg.OnValueChanged(CCVars.ViewportMinimumWidth, _ => UpdateViewportWidthRange());
            _cfg.OnValueChanged(CCVars.ViewportMaximumWidth, _ => UpdateViewportWidthRange());

            UpdateViewportWidthRange();
            UpdateViewportWidthDisplay();
            UpdateViewportScale();
            UpdateApplyButton();
        }


        private void OnShadersCheckBoxToggled(BaseButton.ButtonToggledEventArgs obj)
        {
            UpdateApplyButton();
        }

        private void OnFilmGrainCheckBoxToggled(BaseButton.ButtonToggledEventArgs obj)
        {
            UpdateApplyButton();
        }

        private void OnUIScaleChanged(OptionButton.ItemSelectedEventArgs args)
        {
            UIScaleOption.SelectId(args.Id);
            UpdateApplyButton();
        }

        private void OnHudThemeChanged(OptionButton.ItemSelectedEventArgs args)
        {
            HudThemeOption.SelectId(args.Id);
            UpdateApplyButton();
        }

        private void OnApplyButtonPressed(BaseButton.ButtonEventArgs args)
        {
            _cfg.SetCVar(CVars.DisplayVSync, VSyncCheckBox.Pressed);
            SetConfigLightingQuality(LightingPresetOption.SelectedId);
            if (HudThemeOption.SelectedId != _cfg.GetCVar(CCVars.HudTheme)) // Don't unnecessarily redraw the HUD
            {
                _cfg.SetCVar(CCVars.HudTheme, HudThemeOption.SelectedId);
            }

            _cfg.SetCVar(CVars.DisplayWindowMode,
                         (int) (FullscreenCheckBox.Pressed ? WindowMode.Fullscreen : WindowMode.Windowed));
            _cfg.SetCVar(CVars.DisplayUIScale, UIScaleOptions[UIScaleOption.SelectedId]);
            _cfg.SetCVar(CCVars.ViewportStretch, ViewportStretchCheckBox.Pressed);
            _cfg.SetCVar(CCVars.ViewportFixedScaleFactor, (int) ViewportScaleSlider.Value);
            _cfg.SetCVar(CCVars.ViewportSnapToleranceMargin,
                         IntegerScalingCheckBox.Pressed ? CCVars.ViewportSnapToleranceMargin.DefaultValue : 0);
            _cfg.SetCVar(CCVars.ViewportScaleRender, !ViewportLowResCheckBox.Pressed);
            _cfg.SetCVar(CCVars.ParallaxLowQuality, ParallaxLowQualityCheckBox.Pressed);
            _cfg.SetCVar(CCVars.HudHeldItemShow, ShowHeldItemCheckBox.Pressed);
            _cfg.SetCVar(CCVars.HudFpsCounterVisible, FpsCounterCheckBox.Pressed);
            _cfg.SetCVar(CCVars.ViewportWidth, (int) ViewportWidthSlider.Value);
            _cfg.SetCVar(CCVars.FilmGrain, FilmGrainCheckBox.Pressed);

            if (HudLayoutOption.SelectedMetadata is string opt)
            {
                _cfg.SetCVar(CCVars.UILayout, opt);
            }

            _cfg.SaveToFile();
            UpdateApplyButton();
        }

        private void OnCheckBoxToggled(BaseButton.ButtonToggledEventArgs args)
        {
            UpdateApplyButton();
        }

        private void OnLightingQualityChanged(OptionButton.ItemSelectedEventArgs args)
        {
            LightingPresetOption.SelectId(args.Id);
            UpdateApplyButton();
        }

        private void UpdateApplyButton()
        {
            var isVSyncSame = VSyncCheckBox.Pressed == _cfg.GetCVar(CVars.DisplayVSync);
            var isFullscreenSame = FullscreenCheckBox.Pressed == ConfigIsFullscreen;
            var isLightingQualitySame = LightingPresetOption.SelectedId == GetConfigLightingQuality();
            var isHudThemeSame = HudThemeOption.SelectedId == _cfg.GetCVar(CCVars.HudTheme);
            var isUIScaleSame = MathHelper.CloseToPercent(UIScaleOptions[UIScaleOption.SelectedId], ConfigUIScale);
            var isVPStretchSame = ViewportStretchCheckBox.Pressed == _cfg.GetCVar(CCVars.ViewportStretch);
            var isVPScaleSame = (int) ViewportScaleSlider.Value == _cfg.GetCVar(CCVars.ViewportFixedScaleFactor);
            var isIntegerScalingSame = IntegerScalingCheckBox.Pressed == (_cfg.GetCVar(CCVars.ViewportSnapToleranceMargin) != 0);
            var isVPResSame = ViewportLowResCheckBox.Pressed == !_cfg.GetCVar(CCVars.ViewportScaleRender);
            var isPLQSame = ParallaxLowQualityCheckBox.Pressed == _cfg.GetCVar(CCVars.ParallaxLowQuality);
            var isShowHeldItemSame = ShowHeldItemCheckBox.Pressed == _cfg.GetCVar(CCVars.HudHeldItemShow);
            var isFpsCounterVisibleSame = FpsCounterCheckBox.Pressed == _cfg.GetCVar(CCVars.HudFpsCounterVisible);
            var isWidthSame = (int) ViewportWidthSlider.Value == _cfg.GetCVar(CCVars.ViewportWidth);
            var isLayoutSame = HudLayoutOption.SelectedMetadata is string opt && opt == _cfg.GetCVar(CCVars.UILayout);
            var isFilmGrainSame = FilmGrainCheckBox.Pressed == _cfg.GetCVar(CCVars.FilmGrain);

            ApplyButton.Disabled = isVSyncSame &&
                                   isFullscreenSame &&
                                   isLightingQualitySame &&
                                   isUIScaleSame &&
                                   isVPStretchSame &&
                                   isVPScaleSame &&
                                   isIntegerScalingSame &&
                                   isVPResSame &&
                                   isPLQSame &&
                                   isHudThemeSame &&
                                   isShowHeldItemSame &&
                                   isFpsCounterVisibleSame &&
                                   isWidthSame &&
                                   isLayoutSame &&
                                   isFilmGrainSame;
        }

        private bool ConfigIsFullscreen =>
            _cfg.GetCVar(CVars.DisplayWindowMode) == (int) WindowMode.Fullscreen;

        private float ConfigUIScale => _cfg.GetCVar(CVars.DisplayUIScale);

        private int GetConfigLightingQuality()
        {
            var val = _cfg.GetCVar(CVars.LightResolutionScale);
            var soft = _cfg.GetCVar(CVars.LightSoftShadows);
            if (val <= 0.125)
            {
                return 0;
            }
            else if ((val <= 0.5) && !soft)
            {
                return 1;
            }
            else if (val <= 0.5)
            {
                return 2;
            }
            else
            {
                return 3;
            }
        }

        private void SetConfigLightingQuality(int value)
        {
            switch (value)
            {
                case 0:
                    _cfg.SetCVar(CVars.LightResolutionScale, 0.125f);
                    _cfg.SetCVar(CVars.LightSoftShadows, false);
                    _cfg.SetCVar(CVars.LightBlur, false);
                    break;
                case 1:
                    _cfg.SetCVar(CVars.LightResolutionScale, 0.5f);
                    _cfg.SetCVar(CVars.LightSoftShadows, false);
                    _cfg.SetCVar(CVars.LightBlur, true);
                    break;
                case 2:
                    _cfg.SetCVar(CVars.LightResolutionScale, 0.5f);
                    _cfg.SetCVar(CVars.LightSoftShadows, true);
                    _cfg.SetCVar(CVars.LightBlur, true);
                    break;
                case 3:
                    _cfg.SetCVar(CVars.LightResolutionScale, 1);
                    _cfg.SetCVar(CVars.LightSoftShadows, true);
                    _cfg.SetCVar(CVars.LightBlur, true);
                    break;
            }
        }

        private static int GetConfigUIScalePreset(float value)
        {
            for (var i = 0; i < UIScaleOptions.Length; i++)
            {
                if (MathHelper.CloseToPercent(UIScaleOptions[i], value))
                {
                    return i;
                }
            }

            return 0;
        }

        private void UpdateViewportScale()
        {
            ViewportScaleBox.Visible = !ViewportStretchCheckBox.Pressed;
            IntegerScalingCheckBox.Visible = ViewportStretchCheckBox.Pressed;
            ViewportScaleText.Text = Loc.GetString("ui-options-vp-scale", ("scale", ViewportScaleSlider.Value));
        }

        private void UpdateViewportWidthRange()
        {
            var min = _cfg.GetCVar(CCVars.ViewportMinimumWidth);
            var max = _cfg.GetCVar(CCVars.ViewportMaximumWidth);

            ViewportWidthSlider.MinValue = min;
            ViewportWidthSlider.MaxValue = max;
        }

        private void UpdateViewportWidthDisplay()
        {
            ViewportWidthSliderDisplay.Text = Loc.GetString("ui-options-vp-width", ("width", (int) ViewportWidthSlider.Value));
        }
    }
}
