using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api;
using BTD_Mod_Helper.Api.ModOptions;
using BTD_Mod_Helper.Extensions;
using BTD_Mod_Helper.UI.Menus;
using HeroXpScale;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Unity;
using Newtonsoft.Json.Linq;

[assembly: MelonInfo(typeof(HeroXpScaleMod), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace HeroXpScale;

public class HeroXpScaleMod : BloonsTD6Mod
{
    private const int RoundNumberXpLevel = 4;
    public const float DefaultMin = 1f;
    public const float DefaultMax = 1.71f;
    public const float StepSize = 0.005f;
    private static readonly string BaseHero = TowerType.Quincy;

    public static readonly Dictionary<string, ModSettingFloat> HeroXpScales = new();
    internal static readonly Dictionary<string, double> LoadedXpScales = new();

    public static readonly ModSettingBool BalancedMode = new(true)
    {
        description = $"Keeps the sliders locked to the vanilla low and high of {DefaultMin}x and {DefaultMax}x",
        button = true,
        enabledText = "ON",
        disabledText = "OFF",
        onValueChanged = _ =>
        {
            foreach (var modSetting in HeroXpScales.Values)
            {
                modSetting.min = BalancedMode ? DefaultMin : StepSize;
                modSetting.max = BalancedMode ? DefaultMax : DefaultMin * 2;
                if ((double) modSetting.GetValue() < modSetting.min) modSetting.SetValue(modSetting.min);
                if ((double) modSetting.GetValue() > modSetting.max) modSetting.SetValue(modSetting.max);
            }

            ModContent.GetInstance<ModSettingsMenu>().OnMenuOpened(null);
        }
    };

    /// <summary>
    /// Modify xp scales in relation to base hero
    /// </summary>
    /// <param name="gameModel">The modified game model for a current match</param>
    public override void OnNewGameModel(GameModel gameModel)
    {
        var baseXpCosts = Game.instance.model
            .GetTowersWithBaseId(BaseHero)
            .MaxBy(model => model.tier)!
            .appliedUpgrades
            .Select(Game.instance.model.GetUpgrade)
            .Select(upgrade => upgrade.xpCost)
            .ToArray();

        foreach (var (hero, value) in HeroXpScales)
        {
            var upgrades = gameModel.GetTowersWithBaseId(hero).MaxBy(model => model.tier)!.appliedUpgrades;
            for (var i = 0; i < upgrades.Length; i++)
            {
                gameModel.GetUpgrade(upgrades[i]).xpCost = Math.Max((int) Math.Round(baseXpCosts[i] * value), 1);
            }
        }
    }

    /// <summary>
    /// This load happens early so we need to cache the results from the json and load them later
    /// </summary>
    /// <param name="settings"></param>
    public override void OnLoadSettings(JObject settings)
    {
        foreach (var (key, value) in settings)
        {
            if (key.EndsWith("XpScale") && value?.Value<double>() is { } xpScale)
            {
                LoadedXpScales[key] = xpScale;
            }
        }
    }

    /// <summary>
    /// Compare the Level 4 Xp costs with a base hero (exactly 1000 by default, so gets the clearest ratio)
    /// </summary>
    /// <param name="heroId"></param>
    /// <returns></returns>
    public static float GetXpScale(string heroId)
    {
        var baseXp = Game.instance.model
            .GetHeroWithNameAndLevel(BaseHero, RoundNumberXpLevel)
            .GetAppliedUpgrades().Last().xpCost;
        var heroXp = Game.instance.model
            .GetHeroWithNameAndLevel(heroId, RoundNumberXpLevel)
            .GetAppliedUpgrades().Last().xpCost;
        return heroXp / (float) baseXp;
    }
}

/// <summary>
/// Loads the hero xp scales from the game after the game model loads but before all mod settings get finalized
/// </summary>
public class HeroXpScaleSettings : ModContent
{
    /// <summary>
    /// Register way later
    /// </summary>
    protected override float RegistrationPriority => 42;

    public override void Register()
    {
        var xpScaleText = Localize("XP Scale");
        var defaultText = Localize("Default");
        foreach (var hero in Game.instance.model.heroSet)
        {
            var tower = Game.instance.model.GetTowerWithName(hero.towerId);
            var defaultXpScale = HeroXpScaleMod.GetXpScale(hero.towerId);
            var setting = new ModSettingFloat(defaultXpScale)
            {
                displayName = $"[{hero.towerId}] [{xpScaleText}]",
                description = $"[{defaultText}] {defaultXpScale}",
                slider = true,
                min = HeroXpScaleMod.BalancedMode ? HeroXpScaleMod.DefaultMin : HeroXpScaleMod.StepSize,
                max = HeroXpScaleMod.BalancedMode ? HeroXpScaleMod.DefaultMax : HeroXpScaleMod.DefaultMin * 2,
                stepSize = HeroXpScaleMod.StepSize,
                icon = tower.icon.AssetGUID
            };
            if (HeroXpScaleMod.LoadedXpScales.TryGetValue(hero.towerId + "XpScale", out var xpScale))
            {
                setting.SetValue(xpScale);
            }

            mod.ModSettings[hero.towerId + "XpScale"] = setting;
            HeroXpScaleMod.HeroXpScales[hero.towerId] = setting;
        }
    }
}