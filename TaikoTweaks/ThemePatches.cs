using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using DG.Tweening.Plugins.Core.PathCore;
using HarmonyLib;
using UnityEngine;
using Path = System.IO.Path;

namespace TaikoTweaks;

public class ThemePatches
{
    public static ManualLogSource Log => Plugin.Log;

    private static Dictionary<string, Sprite> _cachedSprite = new();
    private static Dictionary<string, Texture2D> _cachedTexture = new();

    [HarmonyPatch(typeof(SpriteAnimation))]
    [HarmonyPatch("Start")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    public static void SpriteAnimation_Start_Postfix(SpriteAnimation __instance)
    {
        foreach (var animationData in __instance.spriteAnimationData.list)
        {
            for (var i = 0; i < animationData.spriteList.Count; i++)
            {
                var spriteData = animationData.spriteList[i];

                //UnityEngine.Debug.Log($"Sprite loaded: {spriteData.sprite.name}");

                var path = Path.Combine("C:\\Users\\jonas\\Documents\\TakoTako\\theme", spriteData.sprite.name + ".png");
                if (File.Exists(path))
                {
                    if (!_cachedSprite.TryGetValue(spriteData.sprite.name, out var newSprite))
                    {
                        if (!_cachedTexture.TryGetValue(spriteData.sprite.name, out var newTexture))
                        {
                            newTexture = new Texture2D(2, 2);
                            if (!newTexture.LoadImage(File.ReadAllBytes(path)))
                            {
                                Log.LogError($"Could not load sprite from {path}");
                                _cachedTexture.Add(spriteData.sprite.name, null);
                                return;
                            }

                            newTexture.name = spriteData.sprite.name;

                            _cachedTexture.Add(spriteData.sprite.name, newTexture);
                            Log.LogInfo($"Loaded texture: {newTexture.width}, {newTexture.height}");
                        }

                        if (newTexture == null)
                        {
                            Log.LogError("texture was null");
                            return;
                        }

                        Log.LogInfo($"Texture: {spriteData.sprite.name} Rect: {spriteData.sprite.textureRect.x},{spriteData.sprite.textureRect.y} {spriteData.sprite.textureRect.width}x{spriteData.sprite.textureRect.height} Pivot: {spriteData.sprite.pivot.x}x{spriteData.sprite.pivot.y} Ppu: {spriteData.sprite.pixelsPerUnit}");

                        newSprite = Sprite.Create(newTexture, new Rect(0, 0, spriteData.sprite.textureRect.width, spriteData.sprite.textureRect.height), new Vector2(60, 60), 1.0f);
                        newSprite.name = spriteData.sprite.name;
                        _cachedSprite.Add(spriteData.sprite.name, newSprite);
                    }

                    if (newSprite != null)
                        spriteData.sprite = newSprite;
                }
            }
        }
    }
}