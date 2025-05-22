using System;
using BepInEx.Bootstrap;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static WorldItemDropDisplay.ItemDropDisplayManager;
using Object = UnityEngine.Object;

namespace WorldItemDropDisplay;

[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Start))]
static class ItemDropDisplayAttachPatch
{
    static void Postfix(ItemDrop __instance)
    {
        if (__instance.gameObject.GetComponent<DataDisplayBehaviour>() == null)
            __instance.gameObject.AddComponent<DataDisplayBehaviour>();
    }
}

public static class StackPatch
{
    public static event Action<ItemDrop> OnStackChanged = null!;

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.SetStack))]
    static class Patch_SetStack
    {
        static void Postfix(ItemDrop __instance)
        {
            if (!__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
                return;
            OnStackChanged?.Invoke(__instance);
        }
    }
}

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.Awake))]
static class CloneInventoryElementInventoryGridAwakePatch
{
    [HarmonyPriority(Priority.Last)]
    static void Postfix(InventoryGrid __instance)
    {
        Transform? parent = Tutorial.instance?.m_windowRoot?.parent;
        if (parent == null) return;

        // Bail out if we've already created it:
        if (parent.Find(TemplateName) != null) return;

        GameObject? prefab = __instance.m_elementPrefab;
        GameObject? template = Object.Instantiate(prefab, parent);
        template.name = TemplateName;

        Object.Destroy(template.transform.Find("queued").gameObject);
        Object.Destroy(template.transform.Find("selected").gameObject);
        template.transform.Find("binding").GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);


        if (Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.jewelcrafting"))
        {
            Object.Destroy(template.transform.Find("JC_ItemBackground").gameObject);
        }


        template.SetActive(false);
        GameObject bindingGO = template.transform.Find("binding").gameObject;
        TMP_Text? bindingText = bindingGO.GetComponent<TMP_Text>();
        if (bindingText != null)
        {
            bindingGO.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
            bindingGO.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);
            bindingText.enableAutoSizing = true;
            bindingText.autoSizeTextContainer = true;
            bindingText.textWrappingMode = TextWrappingModes.NoWrap;
            bindingText.fontSizeMax = 18;
            bindingText.fontSizeMin = 16;
        }


        // If data display already exists, do nothing, otherwise add component
        if (Tutorial.instance != null && Tutorial.instance.gameObject.GetComponent<ItemDropDisplayManager>() == null)
        {
            Tutorial.instance.gameObject.AddComponent<ItemDropDisplayManager>();
        }
    }
}