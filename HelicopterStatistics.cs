using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

using Rust;
using System;
using System.Collections.Generic;
using Oxide.Core;

using Oxide.Core.Plugins;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Oxide.Plugins
{
    [Info("K014 Test Plugin", "Unknown", 0.101)]
    [Description("Makes epic stuff happen")]

    class HelicopterStats : RustPlugin
    {
        private BaseHelicopter helicopter;
        private HelicopterHpValues helicopterHpValues;
        private UIPanel mainUIPanel;

        private Dictionary<String, PlayerHelicopterStats> playersStats = new Dictionary<String, PlayerHelicopterStats>();

        [Flags]
        public enum AnchorPoints
        {
            Left = 1 << 0,
            Center = 1 << 1,
            Right = 1 << 2,
            Bottom = 1 << 3,
            Middle = 1 << 4,
            Top = 1 << 5
        }

        void Init()
        {
            CreateUI();
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!isBaseCombatEntityHelicopter(entity)) return;

            helicopter = (BaseHelicopter)entity;
            helicopterHpValues = new HelicopterHpValues(helicopter);

            ResetVariables();
            InitUIForAllPlayers();
        }

        private void ResetVariables()
        {
            playersStats.Clear();
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!isBaseCombatEntityHelicopter(entity)) return;

            timer.Once(1f, () =>
            {
                EndUIForAllPlayers();
            });
        }

        void OnEntityTakeDamage(BaseCombatEntity victim, HitInfo hitInfo)
        {
            if (victim == null || hitInfo == null) return;
            if (!isBaseCombatEntityHelicopter(victim)) return;

            BasePlayer attacker = hitInfo?.Initiator?.ToPlayer() ?? null;
            if (attacker == null) return;

            String attackerName = attacker.displayName;
            String weaponName = hitInfo?.Weapon?.GetItem()?.info?.displayName?.english ?? null;

            String bodyPart = "Ni idea weon";
            if (hitInfo.HitBone == 3941181354) bodyPart = "Body";
            if (hitInfo.HitBone == 621403276) bodyPart = "Tail Rotor";
            if (hitInfo.HitBone == 1440332291) bodyPart = "Engine Rotor";
            if (hitInfo.HitBone == 566410933) bodyPart = "Upper Rotor";

            float totalDamage = hitInfo.damageTypes.Total();

            PrintToChat(
                "{0:##.#} ({1:##.#}) hit {2:##.#}, bodyPart: {3}",
                attackerName,
                weaponName,
                totalDamage,
                bodyPart
            );

            NextTick(() =>
            {
                UpdateHpBarsUIForAllUsers();
            });
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            if (helicopter == null) return;

            Puts("projectile: {0}", projectile);
            Puts("projectile?.primaryMagazine: {0}", projectile?.primaryMagazine);
            Puts("projectile?.primaryMagazine?.ammoType: {0}", projectile?.primaryMagazine?.ammoType);
            Puts("projectile?.primaryMagazine?.ammoType?.shortname: {0}", projectile?.primaryMagazine?.ammoType?.shortname);

            ItemDefinition itemDefinition = projectile?.primaryMagazine?.ammoType ?? null;
            if (itemDefinition == null) return;

            if (itemDefinition.shortname == "ammo.rifle")
            {
                if (!playersStats.ContainsKey(player.UserIDString))
                {
                    playersStats.Add(player.UserIDString, new PlayerHelicopterStats());
                }
                playersStats[player.UserIDString].rifleAmmoUsed++;

                UpdateRifleBulletsLabel(player, playersStats[player.UserIDString].rifleAmmoUsed);
            }
            else if (itemDefinition.shortname == "ammo.pistol")
            {
                if (!playersStats.ContainsKey(player.UserIDString))
                {
                    playersStats.Add(player.UserIDString, new PlayerHelicopterStats());
                }
                playersStats[player.UserIDString].pistolAmmoUsed++;

                UpdatePistolBulletsLabel(player, playersStats[player.UserIDString].pistolAmmoUsed);
            }
        }

        private Boolean isBaseCombatEntityHelicopter(BaseNetworkable victim)
        {
            return victim.GetType() == typeof(BaseHelicopter);
        }

        [ChatCommand("killhelis")]
        void TestCommand(BasePlayer player, string command, string[] args)
        {
            KillHelis();
        }

        [ConsoleCommand("hs.killhelis")]
        void ccmdHsKillHelis(ConsoleSystem.Arg arg)
        {
            KillHelis();
        }

        private void KillHelis()
        {
            int helicopterCount = 0;
            BaseHelicopter[] hellicopterList = UnityEngine.Object.FindObjectsOfType<BaseHelicopter>();
            foreach (BaseHelicopter helicopter in hellicopterList)
            {
                helicopter.maxCratesToSpawn = 0;
                helicopterCount++;
                //this triggers the onEntityDeath for heli, unlike DieInstantly()
                helicopter.Hurt(helicopter.MaxHealth());
            }
            if (helicopterCount > 0)
            {
                PrintToChat("{0} Helicopters Have Been Removed", helicopterCount.ToString());
            }
            else
            {
                PrintToChat("No helicopters found :)");
            }
        }

        void InitUIForAllPlayers()
        {
            foreach (var rustPlayer in covalence.Players.All.ToList())
            {
                BasePlayer player = BasePlayer.Find(rustPlayer.Id);
                if (player == null) continue;

                mainUIPanel.Draw(player);

                UpdateHpBarsUI(player);
                UpdateRifleBulletsLabel(player, 0);
                UpdatePistolBulletsLabel(player, 0);
                UpdateMedsLabel(player, 0);
                UpdateTimeLabel(player, 0);
            }
        }

        void EndUIForAllPlayers()
        {
            foreach (var rustPlayer in covalence.Players.All.ToList())
            {
                BasePlayer player = BasePlayer.Find(rustPlayer.Id);
                if (player == null) continue;
                mainUIPanel.DestroyUI(player);
            }
        }

        void UpdateHpBarsUIForAllUsers()
        {
            foreach (var rustPlayer in covalence.Players.All.ToList())
            {
                BasePlayer player = BasePlayer.Find(rustPlayer.Id);
                if (player == null) continue;
                Puts(rustPlayer.ToString());
                Puts(player.ToString());

                UpdateHpBarsUI(player);
            }
        }

        void UpdateHpBarsUI(BasePlayer player)
        {
            helicopterHpValues.UpdateValues();

            UpdateHpHelicopter(player, helicopterHpValues.mainHp, helicopterHpValues.mainHpMax);
            UpdateHpMainRotor(player, helicopterHpValues.mainRotorHp, helicopterHpValues.mainRotorHpMax);
            UpdateHpTailRotor(player, helicopterHpValues.tailRotorHp, helicopterHpValues.tailRotorHpMax);
        }

        private class HelicopterHpValues
        {
            BaseHelicopter helicopter;

            public float mainHp { get; set; }
            public float mainHpMax { get; set; }
            public float mainRotorHp { get; set; }
            public float mainRotorHpMax { get; set; }
            public float tailRotorHp { get; set; }
            public float tailRotorHpMax { get; set; }


            public HelicopterHpValues(BaseHelicopter helicopter)
            {
                this.helicopter = helicopter;
            }

            public void UpdateValues()
            {
                foreach (var weakspot in helicopter.weakspots)
                {
                    mainHp = helicopter.Health();
                    mainHpMax = helicopter.MaxHealth();

                    //tail rotor has only 1 bone
                    if (weakspot.bonenames.Length == 1)
                    {
                        tailRotorHp = weakspot.health;
                        tailRotorHpMax = weakspot.maxHealth;
                    }
                    //main rotor has 2 bones (engine and rotor)
                    else
                    {
                        mainRotorHp = weakspot.health;
                        mainRotorHpMax = weakspot.maxHealth;
                    }
                }
            }
        }

        [ConsoleCommand("qqq")]
        void TestConsoleCommand(ConsoleSystem.Arg arg)
        {
            List<IPlayer> players = covalence.Players.All.ToList();
            foreach (var rustPlayer in players)
            {
                BasePlayer player = BasePlayer.Find(rustPlayer.Id);
                if (player == null) continue;
                mainUIPanel.Draw(player);
            }
        }

        UIPanelText tempText;
        UIPanel tempPanel;
        [ConsoleCommand("www")]
        void TestConsoleCommand2(ConsoleSystem.Arg arg)
        {
            List<IPlayer> players = covalence.Players.All.ToList();
            foreach (var rustPlayer in players)
            {
                BasePlayer player = BasePlayer.Find(rustPlayer.Id);
                if (player == null) continue;
                if (tempPanel == null) tempPanel = mainUIPanel.GetUiPanelByName("hsUI.mainPanel.hpPanel.padding.hpHelicopterContainer.hpForeground");
                if (tempText == null) tempText = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainPanel.hpPanel.padding.hpHelicopterContainer.label");
                tempPanel.size = Vector2.right * 0.2f + Vector2.up * tempPanel.size.y;
                tempText.text = "asdasd";

                tempPanel.Draw(player);
                tempText.Draw(player);
            }

        }

        UIPanel hpBarHelicopter;
        UIPanel hpBarMainRotor;
        UIPanel hpBarTailRotor;
        UIPanelText hpBarHelicopterLabel;
        UIPanelText hpBarMainRotorLabel;
        UIPanelText hpBarTailRotorLabel;

        UIPanelText rifleBulletsLabel;
        UIPanelText pistolBulletsLabel;
        UIPanelText medsLabel;
        UIPanelText timeLabel;

        float hpBarForegroundHeight = 0.94f;

        private void UpdateHpHelicopter(BasePlayer player, float health, float maxHealth)
        {
            hpBarHelicopter.size = new Vector2(health / maxHealth, hpBarForegroundHeight);
            hpBarHelicopterLabel.text = health.ToString("F0");

            hpBarHelicopter.Draw(player);
            hpBarHelicopterLabel.Draw(player);
        }

        private void UpdateHpMainRotor(BasePlayer player, float health, float maxHealth)
        {
            hpBarMainRotor.size = new Vector2(health / maxHealth, hpBarForegroundHeight);
            hpBarMainRotorLabel.text = health.ToString("F0");

            hpBarMainRotor.Draw(player);
            hpBarMainRotorLabel.Draw(player);
        }

        private void UpdateHpTailRotor(BasePlayer player, float health, float maxHealth)
        {
            hpBarTailRotor.size = new Vector2(health / maxHealth, hpBarForegroundHeight);
            hpBarTailRotorLabel.text = health.ToString("F0");

            hpBarTailRotor.Draw(player);
            hpBarTailRotorLabel.Draw(player);
        }

        private void UpdateRifleBulletsLabel(BasePlayer player, int ammount)
        {
            rifleBulletsLabel.text = ammount.ToString();
            rifleBulletsLabel.Draw(player);
        }

        private void UpdatePistolBulletsLabel(BasePlayer player, int ammount)
        {
            pistolBulletsLabel.text = ammount.ToString();
            pistolBulletsLabel.Draw(player);
        }

        private void UpdateMedsLabel(BasePlayer player, int ammount)
        {
            medsLabel.text = ammount.ToString();
            medsLabel.Draw(player);
        }

        private void UpdateTimeLabel(BasePlayer player, int ammount)
        {
            timeLabel.text = ammount.ToString();
            timeLabel.Draw(player);
        }

        private class PlayerHelicopterStats
        {
            public int rifleAmmoUsed { get; set; } = 0;
            public int pistolAmmoUsed { get; set; } = 0;
        }

        private void CreateUI()
        {
            Puts("CreateUI");

            Color panelBackgrounColor = new Color(1f, 1f, 1f, 0.075f);
            Color hpBarBackgrounColor = new Color(0f, 0f, 0f, 0.65f);
            Color hpBarForeground = new Color(0.54f, 0.71f, 0.24f, 0.8f);

            mainUIPanel = new UIPanel("hsUI.mainPanel")
            {
                color = Color.clear,
                size = new Vector2(0.12f, 0.22f),
                offset = new Vector2(0.005f, 0.01f),
                anchorPoint = AnchorPoints.Top | AnchorPoints.Left,
                children = new List<UIPanel>
                {
                    new UIPanel("hsUI.mainPanel.hpPanel")
                    {
                        color = panelBackgrounColor,
                        size = new Vector2(1f, 0.6f),
                        //offset = new Vector2(0.005f, 0.01f),
                        anchorPoint = AnchorPoints.Top | AnchorPoints.Center,
                        children = new List<UIPanel>
                        {
                            new UIPanel("hsUI.mainPanel.hpPanel.padding")
                            {
                                color = Color.clear,
                                size = new Vector2(0.9f, 0.8f),
                                //offset = new Vector2(0.005f, 0.01f),
                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Center,
                                children = new List<UIPanel>
                                {
                                    new UIPanel("hsUI.mainPanel.hpPanel.padding.hpHelicopterContainer")
                                    {
                                        color = Color.clear,
                                        size = new Vector2(1f, 0.3f),
                                        //offset = new Vector2(0.005f, 0.01f),
                                        anchorPoint = AnchorPoints.Top | AnchorPoints.Center,
                                        children = new List<UIPanel>
                                        {
                                            new UIPanel("hsUI.mainPanel.hpPanel.padding.hpHelicopterContainer.icon")
                                            {
                                                color = new Color(1f, 1f, 1f, 0.35f),
                                                size = new Vector2(0.18f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                children = new List<UIPanel>
                                                {
                                                }
                                            },
                                            new UIPanel("hsUI.mainPanel.hpPanel.padding.hpHelicopterContainer.hpBackground")
                                            {
                                                color = hpBarBackgrounColor,
                                                size = new Vector2(0.8f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Right,
                                                children = new List<UIPanel>
                                                {
                                                    new UIPanel("hsUI.mainPanel.hpPanel.padding.hpHelicopterContainer.hpForeground")
                                                    {
                                                        color = hpBarForeground,
                                                        size = new Vector2(0.75f, hpBarForegroundHeight),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        children = new List<UIPanel>
                                                        {
                                                        }
                                                    },
                                                    new UIPanelText("hsUI.mainPanel.hpPanel.padding.hpHelicopterContainer.label")
                                                    {
                                                        size = new Vector2(1f, 1f),
                                                        offset = new Vector2(0.05f, -0.05f),
                                                        //anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        text = "10000",
                                                        align = TextAnchor.MiddleLeft
                                                    }
                                                }
                                            }
                                        }
                                    },
                                    new UIPanel("hsUI.mainPanel.hpPanel.padding.hpMainRotorContainer")
                                    {
                                        color = Color.clear,
                                        size = new Vector2(1f, 0.3f),
                                        //offset = new Vector2(0.005f, 0.01f),
                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Center,
                                        children = new List<UIPanel>
                                        {
                                            new UIPanel("hsUI.mainPanel.hpPanel.padding.hpMainRotorContainer.icon")
                                            {
                                                color = new Color(1f, 1f, 1f, 0.35f),
                                                size = new Vector2(0.18f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                children = new List<UIPanel>
                                                {
                                                }
                                            },
                                            new UIPanel("hsUI.mainPanel.hpPanel.padding.hpMainRotorContainer.hpBackground")
                                            {
                                                color = hpBarBackgrounColor,
                                                size = new Vector2(0.8f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Right,
                                                children = new List<UIPanel>
                                                {
                                                    new UIPanel("hsUI.mainPanel.hpPanel.padding.hpMainRotorContainer.hpForeground")
                                                    {
                                                        color = hpBarForeground,
                                                        size = new Vector2(0.75f, hpBarForegroundHeight),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        children = new List<UIPanel>
                                                        {
                                                        }
                                                    },
                                                    new UIPanelText("hsUI.mainPanel.hpPanel.padding.hpMainRotorContainer.label")
                                                    {
                                                        size = new Vector2(1f, 1f),
                                                        offset = new Vector2(0.05f, -0.05f),
                                                        //anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        text = "value",
                                                        align = TextAnchor.MiddleLeft
                                                    }
                                                }
                                            }
                                        }
                                    },new UIPanel("hsUI.mainPanel.hpPanel.padding.hpTailRotorContainer")
                                    {
                                        color = Color.clear,
                                        size = new Vector2(1f, 0.3f),
                                        //offset = new Vector2(0.005f, 0.01f),
                                        anchorPoint = AnchorPoints.Bottom | AnchorPoints.Center,
                                        children = new List<UIPanel>
                                        {
                                            new UIPanel("hsUI.mainPanel.hpPanel.padding.hpTailRotorContainer.icon")
                                            {
                                                color = new Color(1f, 1f, 1f, 0.35f),
                                                size = new Vector2(0.18f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                children = new List<UIPanel>
                                                {
                                                }
                                            },
                                            new UIPanel("hsUI.mainPanel.hpPanel.padding.hpTailRotorContainer.hpBackground")
                                            {
                                                color = hpBarBackgrounColor,
                                                size = new Vector2(0.8f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Right,
                                                children = new List<UIPanel>
                                                {
                                                    new UIPanel("hsUI.mainPanel.hpPanel.padding.hpTailRotorContainer.hpForeground")
                                                    {
                                                        color = hpBarForeground,
                                                        size = new Vector2(0.75f, hpBarForegroundHeight),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        children = new List<UIPanel>
                                                        {
                                                        }
                                                    },
                                                    new UIPanelText("hsUI.mainPanel.hpPanel.padding.hpTailRotorContainer.label")
                                                    {
                                                        size = new Vector2(1f, 1f),
                                                        offset = new Vector2(0.05f, -0.05f),
                                                        //anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        text = "value",
                                                        align = TextAnchor.MiddleLeft
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                        }
                    },
                    new UIPanel("hsUI.mainPanel.playerStatsPanel")
                    {
                        color = panelBackgrounColor,
                        size = new Vector2(1f, 0.355f),
                        //offset = new Vector2(0.005f, 0.01f),
                        anchorPoint = AnchorPoints.Bottom | AnchorPoints.Center,
                        children = new List<UIPanel>
                        {
                            new UIPanel("hsUI.mainPanel.playerStatsPanel.padding")
                            {
                                color = Color.clear,
                                size = new Vector2(0.9f, 0.8f),
                                //offset = new Vector2(0.005f, 0.01f),
                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Center,
                                children =
                                {
                                    new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.rifleBulletsPanel")
                                    {
                                        color = Color.clear,
                                        size = new Vector2(0.483f, 0.46f),
                                        //offset = new Vector2(0.005f, 0.01f),
                                        anchorPoint = AnchorPoints.Top | AnchorPoints.Left,
                                        children =
                                        {
                                            new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.rifleBulletsPanel.icon")
                                            {
                                                color = new Color(1f, 1f, 1f, 0.35f),
                                                size = new Vector2(0.38f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                children =
                                                {

                                                }
                                            },
                                            new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.rifleBulletsPanel.labelBackground")
                                            {
                                                color = hpBarBackgrounColor,
                                                size = new Vector2(0.58f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                children =
                                                {
                                                    new UIPanelText("hsUI.mainPanel.playerStatsPanel.padding.rifleBulletsPanel.labelBackground.label")
                                                    {
                                                        size = new Vector2(1f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        //anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                        text = "0",
                                                        align = TextAnchor.MiddleCenter
                                                    }
                                                }
                                            }
                                        }
                                    },
                                    new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.pistolBulletsPanel")
                                    {
                                        color = Color.clear,
                                        size = new Vector2(0.483f, 0.46f),
                                        //offset = new Vector2(0.005f, 0.01f),
                                        anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                        children =
                                        {
                                            new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.pistolBulletsPanel.icon")
                                            {
                                                color = new Color(1f, 1f, 1f, 0.35f),
                                                size = new Vector2(0.38f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                children =
                                                {

                                                }
                                            },
                                            new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.pistolBulletsPanel.labelBackground")
                                            {
                                                color = hpBarBackgrounColor,
                                                size = new Vector2(0.58f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                children =
                                                {
                                                    new UIPanelText("hsUI.mainPanel.playerStatsPanel.padding.pistolBulletsPanel.labelBackground.label")
                                                    {
                                                        size = new Vector2(1f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        //anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                        text = "0",
                                                        align = TextAnchor.MiddleCenter
                                                    }
                                                }
                                            }
                                        }
                                    },
                                    new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.medsPanel")
                                    {
                                        color = Color.clear,
                                        size = new Vector2(0.483f, 0.46f),
                                        //offset = new Vector2(0.005f, 0.01f),
                                        anchorPoint = AnchorPoints.Bottom | AnchorPoints.Left,
                                        children =
                                        {
                                            new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.medsPanel.icon")
                                            {
                                                color = new Color(1f, 1f, 1f, 0.35f),
                                                size = new Vector2(0.38f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                children =
                                                {

                                                }
                                            },
                                            new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.medsPanel.labelBackground")
                                            {
                                                color = hpBarBackgrounColor,
                                                size = new Vector2(0.58f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                children =
                                                {
                                                    new UIPanelText("hsUI.mainPanel.playerStatsPanel.padding.medsPanel.labelBackground.label")
                                                    {
                                                        size = new Vector2(1f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        //anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                        text = "0",
                                                        align = TextAnchor.MiddleCenter
                                                    }
                                                }
                                            }
                                        }
                                    },
                                    new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.timePanel")
                                    {
                                        color = Color.clear,
                                        size = new Vector2(0.483f, 0.46f),
                                        //offset = new Vector2(0.005f, 0.01f),
                                        anchorPoint = AnchorPoints.Bottom | AnchorPoints.Right,
                                        children =
                                        {
                                            new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.timePanel.icon")
                                            {
                                                color = new Color(1f, 1f, 1f, 0.35f),
                                                size = new Vector2(0.38f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                children =
                                                {

                                                }
                                            },
                                            new UIPanel("hsUI.mainPanel.playerStatsPanel.padding.timePanel.labelBackground")
                                            {
                                                color = hpBarBackgrounColor,
                                                size = new Vector2(0.58f, 1f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                children =
                                                {
                                                    new UIPanelText("hsUI.mainPanel.playerStatsPanel.padding.timePanel.labelBackground.label")
                                                    {
                                                        size = new Vector2(1f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        //anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                        text = "00:00",
                                                        align = TextAnchor.MiddleCenter
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            hpBarHelicopter = mainUIPanel.GetUiPanelByName("hsUI.mainPanel.hpPanel.padding.hpHelicopterContainer.hpForeground");
            hpBarMainRotor = mainUIPanel.GetUiPanelByName("hsUI.mainPanel.hpPanel.padding.hpMainRotorContainer.hpForeground");
            hpBarTailRotor = mainUIPanel.GetUiPanelByName("hsUI.mainPanel.hpPanel.padding.hpTailRotorContainer.hpForeground");
            hpBarHelicopterLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainPanel.hpPanel.padding.hpHelicopterContainer.label");
            hpBarMainRotorLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainPanel.hpPanel.padding.hpMainRotorContainer.label");
            hpBarTailRotorLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainPanel.hpPanel.padding.hpTailRotorContainer.label");

            rifleBulletsLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainPanel.playerStatsPanel.padding.rifleBulletsPanel.labelBackground.label");
            pistolBulletsLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainPanel.playerStatsPanel.padding.pistolBulletsPanel.labelBackground.label");
            medsLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainPanel.playerStatsPanel.padding.medsPanel.labelBackground.label");
            timeLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainPanel.playerStatsPanel.padding.timePanel.labelBackground.label");
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class UIPanel
        {
            [JsonProperty("name")]
            public string name { get; private set; }

            [JsonProperty("parent")]
            private string parentName { get; set; } = "Hud";

            [JsonProperty("components")]
            private List<ICuiComponent> components = new List<ICuiComponent>();

            public Vector2 size { get; set; } = Vector2.one;
            public Vector2 offset { get; set; } = Vector2.zero;

            public AnchorPoints anchorPoint = AnchorPoints.Bottom | AnchorPoints.Left;

            //Components
            private CuiRectTransformComponent rectTransformComponent;
            protected ICuiColor cuiColorComponent;

            public List<UIPanel> children = new List<UIPanel>();

            protected Color _color = new Color(1f, 0f, 0f, 0.2f);
            public Color color
            {
                get
                {
                    return _color;
                }
                set
                {
                    _color = value;
                    cuiColorComponent.Color = ColorToString(_color);
                }
            }



            public UIPanel(string name)
            {
                this.name = name;
                rectTransformComponent = new CuiRectTransformComponent();
                cuiColorComponent = new CuiImageComponent();
            }

            public void Draw(BasePlayer player)
            {
                DestroyUI(player);
                CuiHelper.AddUi(player, ToJson());
                //LogInfo(ToJson());

                foreach (var child in children)
                {
                    child.Draw(player);
                }
            }

            public void DestroyUI(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, name);
            }

            protected void UpdateComponents()
            {
                UpdateRectTransformComponent();

                components.Clear();
                components.Add((ICuiComponent)cuiColorComponent);
                components.Add(rectTransformComponent);
            }

            protected void UpdateRectTransformComponent()
            {
                Vector2 finalPosition = Vector2.zero;
                if ((anchorPoint & AnchorPoints.Left) == AnchorPoints.Left)
                {
                    finalPosition.x += offset.x;
                }
                if ((anchorPoint & AnchorPoints.Center) == AnchorPoints.Center)
                {
                    finalPosition.x = 0.5f - size.x / 2f + offset.x;
                }
                if ((anchorPoint & AnchorPoints.Right) == AnchorPoints.Right)
                {
                    finalPosition.x = 1f - (size.x + offset.x);
                }
                if ((anchorPoint & AnchorPoints.Bottom) == AnchorPoints.Bottom)
                {
                    finalPosition.y += offset.y;
                }
                if ((anchorPoint & AnchorPoints.Middle) == AnchorPoints.Middle)
                {
                    finalPosition.y = 0.5f - size.y / 2f + offset.y;
                }
                if ((anchorPoint & AnchorPoints.Top) == AnchorPoints.Top)
                {
                    finalPosition.y = 1f - (size.y + offset.y);
                }

                //LogInfo("offset: {0}", offset);
                //LogInfo("anchorPoint: {0}", anchorPoint);
                //LogInfo("finalPosition {0}", finalPosition);

                //LogInfo("(0x1 | 0x32): {0}", (0x1 | 0x32));
                //LogInfo("(0x1 | 0x32) & 0x2: {0}", (0x1 | 0x32) & 0x2);

                //LogInfo("((0 << 1) | (0 << 5)): {0}", ((0 << 1) | (0 << 5)));
                //LogInfo("((0 << 1) | (0 << 5)) & 0 << 2: {0}", ((0 << 1) | (0 << 5)) & 0 << 2);

                rectTransformComponent.AnchorMin = String.Format("{0} {1}", finalPosition.x, finalPosition.y);
                rectTransformComponent.AnchorMax = String.Format("{0} {1}", finalPosition.x + size.x, finalPosition.y + size.y);
            }

            private void UpdateChildren()
            {
                foreach (var child in children)
                {
                    child.parentName = name;
                    child.UpdateChildren();
                }
            }

            public UIPanel GetUiPanelByName(string name)
            {
                //LogInfo("this.name {0}", this.name);
                //LogInfo("name {0}", name);
                if (this.name.Equals(name))
                {
                    //LogInfo("returning this one {0}", this);
                    return this;
                }

                foreach (var child in children)
                {
                    //LogInfo("going into child {0}", child);
                    UIPanel uiPanelRequested = child.GetUiPanelByName(name);
                    if (uiPanelRequested != null) return uiPanelRequested;
                    continue;
                }
                //LogInfo("cant with this, returning null");
                return null;
            }

            #region Json
            public string ToJson()
            {
                UpdateComponents();
                UpdateChildren();

                String json = JsonConvert.SerializeObject(
                    this,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        DefaultValueHandling = DefaultValueHandling.Ignore
                    }
                );

                return $"[{json}]";
            }

            #endregion
        }

        public class UIPanelText : UIPanel
        {
            public string text
            {
                get
                {
                    return textComponent.Text;
                }
                set
                {
                    textComponent.Text = value;
                }
            }

            public TextAnchor align
            {
                get
                {
                    return textComponent.Align;
                }
                set
                {
                    textComponent.Align = value;
                }
            }

            public int fontSize
            {
                get
                {
                    return textComponent.FontSize;
                }
                set
                {
                    textComponent.FontSize = value;
                }
            }

            private CuiTextComponent textComponent
            {
                get
                {
                    return (CuiTextComponent)cuiColorComponent;
                }
                set
                {
                    cuiColorComponent = value;
                }
            }

            public UIPanelText(string name) : base(name)
            {
                textComponent = new CuiTextComponent();
            }
        }

        public class UIPanelRawImage : UIPanel
        {
            public string url
            {
                get
                {
                    return rawImageComponent.Url;
                }
                set
                {
                    rawImageComponent.Url = value;
                }
            }

            private CuiRawImageComponent rawImageComponent
            {
                get
                {
                    return (CuiRawImageComponent)cuiColorComponent;
                }
                set
                {
                    cuiColorComponent = value;
                }
            }

            public UIPanelRawImage(string name) : base(name)
            {
                rawImageComponent = new CuiRawImageComponent();
            }
        }

        #region Util

        public static string ColorToString(Color color)
        {
            return $"{color.r} {color.g} {color.b} {color.a}";
        }

        public static void LogInfo(string format, params object[] args)
        {
            Interface.Oxide.LogInfo("{0}", args.Length > 0 ? string.Format(format, args) : format);
        }

        #endregion
    }
}