using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UITools;
using HarmonyLib;
using ModLoader;
using SFS.IO;
using SFS.Parts;
using SFS.Parts.Modules;

namespace ForcefulFuelTanks
{
    public class Main : Mod
    {
        public static Main main;
        public override string ModNameID => "forcefulfueltanks";
        public override string DisplayName => "Forceful Fuel Tanks";
        public override string Author => "pixelgaming579";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "v1.0";
        public override string Description => "Applies an explosive force on parts in the radius of destroyed fuel tanks.";

        public override Dictionary<string, string> Dependencies { get; } = new Dictionary<string, string> { { "UITools", "1.1.1" } };
        public Dictionary<string, FilePath> UpdatableFiles => new Dictionary<string, FilePath>() { { "https://github.com/pixelgaming579/Forceful-Fuel-Tanks-SFS/releases/latest/download/ForcefulFuelTanks.dll", new FolderPath(ModFolder).ExtendToFile("ForcefulFuelTanks.dll") } };

        public override void Early_Load()
        {
            new Harmony(ModNameID).PatchAll();
            main = this;
        }

        public override void Load()
        {
            if (!Settings.TryLoad())
                Settings.Save();
        }

        public class Settings
        {
            public static Settings settings = new Settings();
            public static FilePath path = new FolderPath(main.ModFolder).ExtendToFile("settings.txt");
            public float destructionRadius = 1.5f;
            public float forceRadius = 25f;
            public float forceStrength = 750f;
            public float duration = 0.1f;
            public bool inheritPartVelocity = true;
            public bool radiusProportionalToFuelLevel = true;
            public bool strengthProportionalToFuelLevel = true;

            public static void Save()
            {
                if (settings == null)
                    settings = new Settings();
                SFS.Parsers.Json.JsonWrapper.SaveAsJson(path, settings, true);
            }

            public static bool TryLoad()
            {
                return SFS.Parsers.Json.JsonWrapper.TryLoadJson(path, out settings);
            }
        }

        public static class Patches
        {
            [HarmonyPatch(typeof(Part), nameof(Part.DestroyPart))]
            class OnPartDestroy
            {
                static void Prefix(Part __instance)
                {
                    if (SceneManager.GetActiveScene().name == "World_PC")
                    {
                        try
                        {
                            ResourceModule module = __instance.GetModules<ResourceModule>().First((ResourceModule m) => m.resourceType.name == "Liquid_Fuel");
                            float fuelLevel = (float) module.ResourceAmount / 10;

                            GameObject go = new GameObject("ForcefulFuelTanks Explosion");
                            go.transform.position = __instance.transform.TransformPoint(__instance.centerOfMass.Value);
                            Explosion explosion = go.AddComponent<Explosion>();

                            explosion.timer = Settings.settings.duration;
                            explosion.rb2d.velocity = Settings.settings.inheritPartVelocity ? __instance.Rocket.rb2d.velocity : Vector2.zero;
                            explosion.forceStrength = Settings.settings.forceStrength * (Settings.settings.strengthProportionalToFuelLevel ? fuelLevel : 1);
                            explosion.forceTrigger.radius = Settings.settings.forceRadius * (Settings.settings.radiusProportionalToFuelLevel ? fuelLevel : 1);
                            explosion.destructionCollider.radius = Settings.settings.destructionRadius * (Settings.settings.radiusProportionalToFuelLevel ? fuelLevel : 1);
                        }
                        catch (InvalidOperationException) {}
                    }
                }
            }
        }

        public class Explosion : MonoBehaviour
        {
            public float timer;
            public float forceStrength;
            public Rigidbody2D rb2d;
            public CircleCollider2D destructionCollider;
            public CircleCollider2D forceTrigger;
            HashSet<Part> partsHit;

            void Awake()
            {
                gameObject.layer = LayerMask.NameToLayer("Parts");
                rb2d = gameObject.AddComponent<Rigidbody2D>();
                rb2d.bodyType = RigidbodyType2D.Kinematic;
                destructionCollider = gameObject.AddComponent<CircleCollider2D>();
                forceTrigger = gameObject.AddComponent<CircleCollider2D>();
                forceTrigger.isTrigger = true;
                partsHit = new HashSet<Part>();
            }

            void OnCollisionEnter2D(Collision2D collision)
            {
                if (collision.otherCollider == destructionCollider && collision.collider.TryGetComponent(out Part part))
                {
                    part.DestroyPart(true, true, SFS.World.DestructionReason.RocketCollision);
                }
            }

            void OnTriggerEnter2D(Collider2D other)
            {
                if (other.TryGetComponent(out Part part))
                {
                    partsHit.Add(part);
                }
            }
        
            void OnTriggerExit2D(Collider2D other)
            {
                if (other.TryGetComponent(out Part part))
                {
                    partsHit.Remove(part);
                }
            }

            void FixedUpdate()
            {
                timer -= Time.fixedDeltaTime;
                if (timer <= 0)
                {
                    Destroy(gameObject);
                }
                else
                {
                    foreach (Part part in partsHit.Where((Part part) => part != null))
                    {
                        Vector3 pos = part.transform.TransformPoint(part.centerOfMass.Value);
                        Vector3 direction = pos - transform.position;
                        Vector3 force = forceStrength * direction.normalized;
                        part.Rocket.rb2d.AddForceAtPosition(force, pos);
                    }
                }
            }
        }
    }
}