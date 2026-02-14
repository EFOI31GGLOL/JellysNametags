using BepInEx;
using Photon.Pun;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace FPSNameTags
{
    [BepInPlugin("com.goldenthropy.fpsnametags", "FPS NameTags", "1.0.0")]
    public class FPSNameTagsPlugin : BaseUnityPlugin
    {
        private static readonly Dictionary<VRRig, GameObject> fpsNametags = new Dictionary<VRRig, GameObject>();
        private static FieldInfo fpsField;
        private static FieldInfo rawCosmeticsField;
        private static FieldInfo playerNameField;
        private float nextUpdate = 0f;
        private const float REFRESH_RATE = 0.15f;
        private Dictionary<string, string> dateCache = new Dictionary<string, string>();

        void Awake()
        {
            fpsField = typeof(VRRig).GetField("fps", BindingFlags.Instance | BindingFlags.NonPublic);

            rawCosmeticsField = typeof(VRRig).GetField("rawCosmeticString",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            playerNameField = typeof(VRRig).GetField("playerText",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Logger.LogInfo("ha valaki magyar es nezi a kodot dogolj meg ez az enyem nem skidelsz");
        }

        void Update()
        {
            if (!PhotonNetwork.InRoom)
            {
                cleanupAllTags();
                return;
            }

            List<VRRig> toRemove = new List<VRRig>();
            foreach (var kv in fpsNametags)
            {
                if (!GorillaParent.instance.vrrigs.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }
            foreach (var rig in toRemove)
            {
                if (fpsNametags[rig] != null)
                    Destroy(fpsNametags[rig]);
                fpsNametags.Remove(rig);
            }

            bool tick = Time.realtimeSinceStartup >= nextUpdate;
            if (tick)
                nextUpdate = Time.realtimeSinceStartup + REFRESH_RATE;

            foreach (VRRig vrrig in GorillaParent.instance.vrrigs)
            {
                if (vrrig.isLocal)
                    continue;

                if (!fpsNametags.ContainsKey(vrrig))
                {
                    GameObject go = new GameObject("nametag");
                    TextMeshPro tmp = go.AddComponent<TextMeshPro>();
                    tmp.fontSize = 4.8f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    go.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                    fpsNametags.Add(vrrig, go);
                }

                GameObject nameTag = fpsNametags[vrrig];
                TextMeshPro textMesh = nameTag.GetComponent<TextMeshPro>();

                if (tick)
                {
                    string playerName = getPlayerName(vrrig);

                    int rawFps = fpsField != null ? (int)fpsField.GetValue(vrrig) : 0;
                    int fps = Mathf.Clamp(rawFps, 0, 999);
                    string fpsColorHex = getFpsColorHex(fps);
                    string platform = getPlatformShort(vrrig);
                    string joined = vrrig.OwningNetPlayer != null ? getJoinedDate(vrrig.OwningNetPlayer.UserId) : "n/a";

                    textMesh.text =
                        $"<color=#FFFFFF><b>{playerName}</b></color>\n" +
                        $"<size=80%><color={fpsColorHex}>{fps} FPS</color></size>\n" +
                        $"<size=60%><color=#AAAAAA>{platform} | {joined}</color></size>";
                }

                float scaleFactor = vrrig.scaleFactor;
                nameTag.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f) * scaleFactor;
                nameTag.transform.position = vrrig.headMesh.transform.position
                    + vrrig.headMesh.transform.up * (0.4f * scaleFactor);

                if (Camera.main != null)
                {
                    nameTag.transform.LookAt(Camera.main.transform.position);
                    nameTag.transform.Rotate(0f, 180f, 0f);
                }
            }
        }

        private string getPlayerName(VRRig vrrig)
        {
            string name = playerNameField?.GetValue(vrrig) as string;
            if (!string.IsNullOrEmpty(name))
                return name;

            if (vrrig.OwningNetPlayer != null)
            {
                string nick = vrrig.OwningNetPlayer.NickName;
                if (!string.IsNullOrEmpty(nick))
                    return nick;
            }

            return "unknown";
        }

        private string getFpsColorHex(int fps)
        {
            if (fps >= 70) return "#66FF66";
            if (fps >= 60) return "#FFE033";
            if (fps <= 0) return "#FFFFFF";
            return "#FF4444";
        }

        private string getPlatformShort(VRRig rig)
        {
            string cosmetics = (rawCosmeticsField?.GetValue(rig) as string) ?? "";
            int propCount = rig.OwningNetPlayer != null
                ? rig.OwningNetPlayer.GetPlayerRef().CustomProperties.Count
                : 0;

            if (cosmetics.Contains("S. FIRST LOGIN")) return "STEAM";
            if (cosmetics.Contains("FIRST LOGIN") || propCount >= 2) return "PC";
            return "META";
        }

        private string getJoinedDate(string userId)
        {
            if (dateCache.TryGetValue(userId, out string cached))
                return cached;

            dateCache[userId] = "...";
            PlayFabClientAPI.GetAccountInfo(
                new GetAccountInfoRequest { PlayFabId = userId },
                result => {
                    DateTime created = result.AccountInfo.Created;
                    dateCache[userId] = created.ToString("dd/MM/yyyy");
                },
                error => { dateCache[userId] = "?"; }
            );
            return dateCache[userId];
        }

        private void cleanupAllTags()
        {
            foreach (var kv in fpsNametags)
            {
                if (kv.Value != null)
                    Destroy(kv.Value);
            }
            fpsNametags.Clear();
            dateCache.Clear();
        }

        void OnDestroy()
        {
            cleanupAllTags();
        }
    }
}