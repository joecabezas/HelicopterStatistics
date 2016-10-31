using System;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using UnityEngine;

using Rust;
using System;
using System.Collections.Generic;
using Oxide.Core;

using Oxide.Core.Plugins;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using System.Reflection;

namespace Oxide.Plugins
{
    [Info("Helicopter Statistics", "k014", 0.2)]
    [Description("Shows statictics for the helicopter like hp, bullets spent, etc")]

    class HelicopterStatistics : RustPlugin
    {
        private static float TIME_TO_HIDE_UI_AFTER_HELICOPTER_KILLED = 20f;

        private BaseHelicopter helicopter;
        private HelicopterHpValues helicopterHpValues;
        private UIPanel mainUIPanel;
        private UInt32 helicopterEventSeconds;
        private Timer helicopterEventTimer;
        private Timer hideUIAfterHelicopterDeadTimer;

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
            PreventHelicopterToFireRockets();

            ResetVariables();
            InitUIForAllPlayers();
        }

        private void ResetVariables()
        {
            playersStats.Clear();
        }

        private void PreventHelicopterToFireRockets()
        {
            FieldInfo maxRockets = typeof(PatrolHelicopterAI).GetField("maxRockets", BindingFlags.NonPublic | BindingFlags.Instance);
            var heliAI = helicopter.GetComponent<PatrolHelicopterAI>() ?? null;
            maxRockets.SetValue(heliAI, 0);
            helicopter.SendNetworkUpdateImmediate(true);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!isBaseCombatEntityHelicopter(entity)) return;

            if (helicopterEventTimer != null)
            {
                helicopterEventTimer.Destroy();
                helicopterEventTimer = null;
            }

            hideUIAfterHelicopterDeadTimer = timer.Once(TIME_TO_HIDE_UI_AFTER_HELICOPTER_KILLED, () =>
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
                "[{0:##.#}]<color=gray>[{1:##.#}]</color><color=red>[{2:##.#}dmg]</color><color=green>[{3}]</color>",
                attackerName,
                weaponName,
                totalDamage,
                bodyPart
            );

            NextTick(() =>
            {
                UpdateHpBarsUIForAllUsers();
                UpdateTimerForAllUsers();
            });
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            if (helicopter == null) return;

            ItemDefinition itemDefinition = projectile?.primaryMagazine?.ammoType ?? null;
            if (itemDefinition == null) return;

            if (itemDefinition.shortname == "ammo.rifle")
            {
                GetPlayerStatsByUserIdString(player.UserIDString).rifleAmmoUsed++;
                UpdateRifleBulletsLabel(player, GetPlayerStatsByUserIdString(player.UserIDString).rifleAmmoUsed);
            }
            else if (itemDefinition.shortname == "ammo.pistol")
            {
                GetPlayerStatsByUserIdString(player.UserIDString).pistolAmmoUsed++;
                UpdatePistolBulletsLabel(player, GetPlayerStatsByUserIdString(player.UserIDString).pistolAmmoUsed);
            }
        }

        private void OnHealingItemUse(HeldEntity item, BasePlayer player)
        {
            if (helicopter == null) return;

            if (item.ShortPrefabName.Equals("syringe_medical.entity"))
            {
                GetPlayerStatsByUserIdString(player.UserIDString).syringesUsed++;
                UpdateMedsLabel(player, GetPlayerStatsByUserIdString(player.UserIDString).syringesUsed);
            }
        }

        private PlayerHelicopterStats GetPlayerStatsByUserIdString(String id)
        {
            if (!playersStats.ContainsKey(id))
            {
                playersStats.Add(id, new PlayerHelicopterStats());
            }
            return playersStats[id];
        }

        private Boolean isBaseCombatEntityHelicopter(BaseNetworkable victim)
        {
            return victim.GetType() == typeof(BaseHelicopter);
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

        #region Commands

        [ConsoleCommand("qqq")]
        void TestConsoleCommand(ConsoleSystem.Arg arg)
        {
            List<BasePlayer> players = BasePlayer.activePlayerList;
            foreach (var player in players)
            {
                if (player == null) continue;
                mainUIPanel.Draw(player);
            }
        }

        [ConsoleCommand("www")]
        void TestConsoleCommand2(ConsoleSystem.Arg arg)
        {
            helicopterEventTimer.Destroy();
        }

        private UInt32 GetSecondsFromTimeStamp(UInt32 timeStamp)
        {
            return timeStamp % 60;
        }

        private UInt32 GetMinutesFromTimeStamp(UInt32 timeStamp)
        {
            return timeStamp / 60;
        }

        #endregion

        #region UI Updaters

        void InitUIForAllPlayers()
        {
            if (hideUIAfterHelicopterDeadTimer != null)
            {
                hideUIAfterHelicopterDeadTimer.Destroy();
                hideUIAfterHelicopterDeadTimer = null;
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null) continue;

                mainUIPanel.Draw(player);

                UpdateHpBarsUI(player);
                UpdateRifleBulletsLabel(player, 0);
                UpdatePistolBulletsLabel(player, 0);
                UpdateMedsLabel(player, 0);
                UpdateTimeLabel(player, "00:00");
            }
        }

        void EndUIForAllPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null) continue;
                mainUIPanel.DestroyUI(player);
            }
        }

        void UpdateHpBarsUIForAllUsers()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null) continue;
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

        private void UpdateTimerForAllUsers()
        {
            if (helicopterEventTimer != null) return;

            helicopterEventSeconds = 0;

            Action startTimerAction = () => {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null) continue;

                    timerStartedPanel.active = true;
                    timerStartedPanel.Draw(player);
                    timer.Once(5f, () =>
                    {
                        timerStartedPanel.active = false;
                        timerStartedPanel.DestroyUI(player);
                    });

                    string timerText = string.Format(
                        "{0:00}:{1:00}",
                        GetMinutesFromTimeStamp((uint)this.helicopterEventSeconds),
                        GetSecondsFromTimeStamp((uint)this.helicopterEventSeconds)
                    );
                    UpdateTimeLabel(player, timerText);
                }
            };

            startTimerAction();
            helicopterEventTimer = timer.Every(1f, () => {
                this.helicopterEventSeconds++;
                startTimerAction();
            });
        }

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

        private void UpdateTimeLabel(BasePlayer player, string text)
        {
            timeLabel.text = text;
            timeLabel.Draw(player);
        }

        #endregion

        #region UI

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

        UIPanel timerStartedPanel;

        float hpBarForegroundHeight = 0.95f;

        private void CreateUI()
        {
            Puts("CreateUI");

            Color panelBackgrounColor = new Color(1f, 1f, 1f, 0.075f);
            Color hpBarBackgrounColor = new Color(0f, 0f, 0f, 0.65f);
            Color hpBarForegroundColor = new Color(0.54f, 0.71f, 0.24f, 0.8f);
            
            float statsPanelWidth = 0.6f;


            mainUIPanel = new UIPanel("hsUI.mainCanvas")
            {
                color = Color.clear,
                size = new Vector2(1f, 1f),
                children = new List<UIPanel>
                {
                    new UIPanel("hsUI.mainCanvas.mainPanel")
                    {
                        color = Color.clear,
                        size = new Vector2(0.2f, 0.22f),
                        anchorPoint = AnchorPoints.Top | AnchorPoints.Left,
                        offset = new Vector2(0.0045f, 0.01f),
                        children = new List<UIPanel>
                        {
                            new UIPanel("hsUI.mainCanvas.mainPanel.eventStartedPanel")
                            {
                                color = hpBarForegroundColor,
                                size = new Vector2(0.37f, 0.1f),
                                anchorPoint = AnchorPoints.Bottom | AnchorPoints.Right,
                                offset = new Vector2(0f, 0.0555f),
                                active = false,
                                children = new List<UIPanel>
                                {
                                    new UIPanelText("hsUI.mainCanvas.mainPanel.eventStartedPanel.label")
                                    {
                                        text = "Timer Started!",
                                        align = TextAnchor.MiddleCenter,
                                        fontSize = 12
                                    }
                                }
                            },
                            new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel")
                            {
                                color = panelBackgrounColor,
                                size = new Vector2(statsPanelWidth, 0.6f),
                                anchorPoint = AnchorPoints.Top | AnchorPoints.Left,
                                children = new List<UIPanel>
                                {
                                    new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding")
                                    {
                                        color = Color.clear,
                                        size = new Vector2(0.9f, 0.8f),
                                        //offset = new Vector2(0.005f, 0.01f),
                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Center,
                                        children = new List<UIPanel>
                                        {
                                            new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpHelicopterContainer")
                                            {
                                                color = Color.clear,
                                                size = new Vector2(1f, 0.3f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Top | AnchorPoints.Center,
                                                children = new List<UIPanel>
                                                {
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpHelicopterContainer.icon")
                                                    {
                                                        color = new Color(1f, 1f, 1f, 0.35f),
                                                        size = new Vector2(0.18f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        children = new List<UIPanel>
                                                        {
                                                        }
                                                    },
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpHelicopterContainer.hpBackground")
                                                    {
                                                        color = hpBarBackgrounColor,
                                                        size = new Vector2(0.8f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Right,
                                                        children = new List<UIPanel>
                                                        {
                                                            new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpHelicopterContainer.hpForeground")
                                                            {
                                                                color = hpBarForegroundColor,
                                                                size = new Vector2(0.75f, hpBarForegroundHeight),
                                                                //offset = new Vector2(0.005f, 0.01f),
                                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                                children = new List<UIPanel>
                                                                {
                                                                }
                                                            },
                                                            new UIPanelText("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpHelicopterContainer.label")
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
                                            new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpMainRotorContainer")
                                            {
                                                color = Color.clear,
                                                size = new Vector2(1f, 0.3f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Center,
                                                children = new List<UIPanel>
                                                {
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpMainRotorContainer.icon")
                                                    {
                                                        color = new Color(1f, 1f, 1f, 0.35f),
                                                        size = new Vector2(0.18f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        children = new List<UIPanel>
                                                        {
                                                        }
                                                    },
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpMainRotorContainer.hpBackground")
                                                    {
                                                        color = hpBarBackgrounColor,
                                                        size = new Vector2(0.8f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Right,
                                                        children = new List<UIPanel>
                                                        {
                                                            new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpMainRotorContainer.hpForeground")
                                                            {
                                                                color = hpBarForegroundColor,
                                                                size = new Vector2(0.75f, hpBarForegroundHeight),
                                                                //offset = new Vector2(0.005f, 0.01f),
                                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                                children = new List<UIPanel>
                                                                {
                                                                }
                                                            },
                                                            new UIPanelText("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpMainRotorContainer.label")
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
                                            },new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpTailRotorContainer")
                                            {
                                                color = Color.clear,
                                                size = new Vector2(1f, 0.3f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Bottom | AnchorPoints.Center,
                                                children = new List<UIPanel>
                                                {
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpTailRotorContainer.icon")
                                                    {
                                                        color = new Color(1f, 1f, 1f, 0.35f),
                                                        size = new Vector2(0.18f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        children = new List<UIPanel>
                                                        {
                                                        }
                                                    },
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpTailRotorContainer.hpBackground")
                                                    {
                                                        color = hpBarBackgrounColor,
                                                        size = new Vector2(0.8f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Right,
                                                        children = new List<UIPanel>
                                                        {
                                                            new UIPanel("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpTailRotorContainer.hpForeground")
                                                            {
                                                                color = hpBarForegroundColor,
                                                                size = new Vector2(0.75f, hpBarForegroundHeight),
                                                                //offset = new Vector2(0.005f, 0.01f),
                                                                anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                                children = new List<UIPanel>
                                                                {
                                                                }
                                                            },
                                                            new UIPanelText("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpTailRotorContainer.label")
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
                            new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel")
                            {
                                color = panelBackgrounColor,
                                size = new Vector2(statsPanelWidth, 0.355f),
                                //offset = new Vector2(0.005f, 0.01f),
                                anchorPoint = AnchorPoints.Bottom | AnchorPoints.Left,
                                children = new List<UIPanel>
                                {
                                    new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding")
                                    {
                                        color = Color.clear,
                                        size = new Vector2(0.9f, 0.8f),
                                        //offset = new Vector2(0.005f, 0.01f),
                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Center,
                                        children =
                                        {
                                            new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.rifleBulletsPanel")
                                            {
                                                color = Color.clear,
                                                size = new Vector2(0.483f, 0.46f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Top | AnchorPoints.Left,
                                                children =
                                                {
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.rifleBulletsPanel.icon")
                                                    {
                                                        color = new Color(1f, 1f, 1f, 0.35f),
                                                        size = new Vector2(0.38f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        children =
                                                        {

                                                        }
                                                    },
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.rifleBulletsPanel.labelBackground")
                                                    {
                                                        color = hpBarBackgrounColor,
                                                        size = new Vector2(0.58f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                        children =
                                                        {
                                                            new UIPanelText("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.rifleBulletsPanel.labelBackground.label")
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
                                            new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.pistolBulletsPanel")
                                            {
                                                color = Color.clear,
                                                size = new Vector2(0.483f, 0.46f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                children =
                                                {
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.pistolBulletsPanel.icon")
                                                    {
                                                        color = new Color(1f, 1f, 1f, 0.35f),
                                                        size = new Vector2(0.38f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        children =
                                                        {

                                                        }
                                                    },
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.pistolBulletsPanel.labelBackground")
                                                    {
                                                        color = hpBarBackgrounColor,
                                                        size = new Vector2(0.58f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                        children =
                                                        {
                                                            new UIPanelText("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.pistolBulletsPanel.labelBackground.label")
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
                                            new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.medsPanel")
                                            {
                                                color = Color.clear,
                                                size = new Vector2(0.483f, 0.46f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Bottom | AnchorPoints.Left,
                                                children =
                                                {
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.medsPanel.icon")
                                                    {
                                                        color = new Color(1f, 1f, 1f, 0.35f),
                                                        size = new Vector2(0.38f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        children =
                                                        {

                                                        }
                                                    },
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.medsPanel.labelBackground")
                                                    {
                                                        color = hpBarBackgrounColor,
                                                        size = new Vector2(0.58f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                        children =
                                                        {
                                                            new UIPanelText("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.medsPanel.labelBackground.label")
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
                                            new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.timePanel")
                                            {
                                                color = Color.clear,
                                                size = new Vector2(0.483f, 0.46f),
                                                //offset = new Vector2(0.005f, 0.01f),
                                                anchorPoint = AnchorPoints.Bottom | AnchorPoints.Right,
                                                children =
                                                {
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.timePanel.icon")
                                                    {
                                                        color = new Color(1f, 1f, 1f, 0.35f),
                                                        size = new Vector2(0.38f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Middle | AnchorPoints.Left,
                                                        children =
                                                        {

                                                        }
                                                    },
                                                    new UIPanel("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.timePanel.labelBackground")
                                                    {
                                                        color = hpBarBackgrounColor,
                                                        size = new Vector2(0.58f, 1f),
                                                        //offset = new Vector2(0.005f, 0.01f),
                                                        anchorPoint = AnchorPoints.Top | AnchorPoints.Right,
                                                        children =
                                                        {
                                                            new UIPanelText("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.timePanel.labelBackground.label")
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
                    }
                }
            };

            hpBarHelicopter = mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpHelicopterContainer.hpForeground");
            hpBarMainRotor = mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpMainRotorContainer.hpForeground");
            hpBarTailRotor = mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpTailRotorContainer.hpForeground");
            hpBarHelicopterLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpHelicopterContainer.label");
            hpBarMainRotorLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpMainRotorContainer.label");
            hpBarTailRotorLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.hpPanel.padding.hpTailRotorContainer.label");

            rifleBulletsLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.rifleBulletsPanel.labelBackground.label");
            pistolBulletsLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.pistolBulletsPanel.labelBackground.label");
            medsLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.medsPanel.labelBackground.label");
            timeLabel = (UIPanelText)mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.playerStatsPanel.padding.timePanel.labelBackground.label");

            timerStartedPanel = mainUIPanel.GetUiPanelByName("hsUI.mainCanvas.mainPanel.eventStartedPanel");
        }

        #endregion

        #region Classes

        [JsonObject(MemberSerialization.OptIn)]
        private class UIPanel
        {
            [JsonProperty("name")]
            public string name { get; private set; }

            [JsonProperty("parent")]
            private string parentName { get; set; } = "Hud";

            [JsonProperty("components")]
            private List<ICuiComponent> components = new List<ICuiComponent>();

            public Vector2 size { get; set; } = Vector2.one;
            public Vector2 offset { get; set; } = Vector2.zero;

            public bool active { get; set; } = true;

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
                if (!active) return;

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

        private class UIPanelText : UIPanel
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

        private class UIPanelRawImage : UIPanel
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

        private class PlayerHelicopterStats
        {
            public int rifleAmmoUsed { get; set; } = 0;
            public int pistolAmmoUsed { get; set; } = 0;
            public int syringesUsed { get; set; } = 0;
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

        #endregion

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