using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using static WorldItemDropDisplay.WorldItemDropDisplayPlugin;
using Object = UnityEngine.Object;

namespace WorldItemDropDisplay;

public class ItemDropDisplayManager : MonoBehaviour
{
    public static ItemDropDisplayManager Instance { get; private set; } = null!;

    [Tooltip("How often (seconds) to refresh positions")] [SerializeField]
    private float positionInterval = ItemPositionInterval.Value;

    [Tooltip("How often (seconds) to refresh data (stack/quality)")] [SerializeField]
    private float dataInterval = ItemDataInterval.Value;

    [Tooltip("Maximum distance to show the display")] [SerializeField]
    private float maxDisplayDistance = ItemMaxDisplayDistance.Value;

    [Tooltip("Vertical offset above the item")] [SerializeField]
    private Vector3 worldOffset = ItemWorldOffset.Value;

    private GameObject template = null!;
    private Transform uiRoot = null!;
    private Camera cam = null!;
    private Transform playerTransform = null!;
    private float maxDistSqr;
    private float posTimer;
    private float dataTimer;

    public readonly Color m_foodEitrColor = new(0.6f, 0.6f, 1f, 1f);
    public readonly Color m_foodHealthColor = new(1f, 0.5f, 0.5f, 1f);
    public readonly Color m_foodStaminaColor = new(1f, 1f, 0.5f, 1f);

    private class WorldItem
    {
        public ItemDrop itemDrop = null!;
        internal Vector2i m_pos;
        internal GameObject m_go = null!;
        internal RectTransform m_rt = null!;
        internal Image m_icon = null!;
        internal TMP_Text m_amount = null!;
        internal TMP_Text m_quality = null!;
        internal Image m_equiped = null!;
        internal Image m_noteleport = null!;
        internal Image m_food = null!;
        internal GuiBar m_durability = null!;

        // caching last‐seen values:
        public int lastStack;
        public int lastQuality;
        public float lastDurPct;
        public Sprite lastIcon = null!;
        public Color lastFoodColor;
        public bool dataDirty;
    }

    private readonly List<WorldItem> worldItems = new();

    //private readonly Dictionary<ItemDrop, WorldItem> _items = new Dictionary<ItemDrop, WorldItem>();
    public IObjectPool<GameObject> pool = null!;


    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        maxDistSqr = maxDisplayDistance * maxDisplayDistance;

        StackPatch.OnStackChanged += OnAnyStackChanged;
    }

    private void Start()
    {
        cam = Utils.GetMainCamera();

        // find the template we made in the patch
        template = Tutorial.instance.m_windowRoot.parent.transform.Find("DataDisplayTemplate").gameObject;
        if (template == null)
        {
            Debug.LogError("[ItemDropDisplayManager] DataDisplayTemplate not found!");
            enabled = false;
            return;
        }

        uiRoot = template.transform.parent;

        pool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                // instantiate disabled clone of template
                GameObject? go = Instantiate(template, uiRoot);
                var rt = go.GetComponent<RectTransform>();
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                go.SetActive(false);
                return go;
            },
            //actionOnGet: go => go.SetActive(true),
            actionOnRelease: go => go.SetActive(false),
            actionOnDestroy: go => Destroy(go),
            collectionCheck: false,
            defaultCapacity: 16,
            maxSize: 512
        );
    }

    private void Update()
    {
        posTimer += Time.deltaTime;
        dataTimer += Time.deltaTime;


        if (posTimer >= positionInterval)
        {
            posTimer -= positionInterval;
            UpdatePositions();
        }

        if (!(dataTimer >= dataInterval)) return;
        dataTimer -= dataInterval;
        UpdateData();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        StackPatch.OnStackChanged -= OnAnyStackChanged;
        Instance = null!;
    }

    public void Register(ItemDrop drop)
    {
        if (drop == null || template == null) return;
        if (worldItems.Exists(x => x.itemDrop == drop)) return;

        var go = pool.Get();
        go.name = "DataDisplay_" + drop.GetInstanceID();
        Transform goTransform = go.transform;
        WorldItem worldItem = new()
        {
            itemDrop = drop,
            m_go = go,
            m_rt = go.GetComponent<RectTransform>(),
            m_icon = goTransform.Find("icon").GetComponent<Image>(),
            m_amount = goTransform.Find("amount").GetComponent<TMP_Text>(),
            m_quality = goTransform.Find("quality").GetComponent<TMP_Text>(),
            m_equiped = goTransform.Find("equiped").GetComponent<Image>(),
            m_noteleport = goTransform.Find("noteleport").GetComponent<Image>(),
            m_food = goTransform.Find("foodicon").GetComponent<Image>(),
            m_durability = goTransform.Find("durability").GetComponent<GuiBar>(),
            dataDirty = true // force full paint on first UpdateData
        };
        worldItems.Add(worldItem);
    }

    public void Unregister(ItemDrop drop)
    {
        int idx = worldItems.FindIndex(x => x.itemDrop == drop);
        if (idx < 0) return;
        pool.Release(worldItems[idx].m_go);
        worldItems.RemoveAt(idx);
    }

    public void OnAnyStackChanged(ItemDrop drop)
    {
        WorldItem? worldItem = worldItems.Find(x => x.itemDrop == drop);
        if (worldItem != null && worldItem.itemDrop != null)
        {
            worldItem.dataDirty = true;
        }
    }

    private void UpdatePositions()
    {
        if (cam == null) cam = Utils.GetMainCamera();
        if (playerTransform == null && Player.m_localPlayer != null)
            playerTransform = Player.m_localPlayer.transform;
        if (cam == null || playerTransform == null || worldItems.Count == 0) return;

        Vector3 playerPos = playerTransform.position;

        foreach (WorldItem? worldItem in worldItems)
        {
            if (worldItem == null) continue;
            ItemDrop? drop = worldItem.itemDrop;
            if (drop == null) continue;

            GameObject worldItemGo = worldItem.m_go;

            if (InventoryGui.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible() || Hud.IsPieceSelectionVisible() || !Player.m_localPlayer!.TakeInput())
            {
                if (worldItemGo.activeSelf) worldItemGo.SetActive(false);
                continue;
            }


            // Exclude fish to prevent fishing exploit
            if (drop.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Fish)
            {
                Fish? fish = drop.gameObject.GetComponent<Fish>();
                if (!fish.IsOutOfWater())
                {
                    if (worldItemGo.activeSelf) worldItemGo.SetActive(false);
                    continue;
                }
            }

            // distance cull
            Vector3 dv = drop.transform.position - playerPos;
            if (dv.sqrMagnitude > maxDistSqr)
            {
                if (worldItemGo.activeSelf) worldItemGo.SetActive(false);
                continue;
            }

            // world→screen
            Vector3 sp = cam.WorldToScreenPointScaled(drop.transform.position + worldOffset - (SubtractCamOffset.Value.IsOn() ? GameCamera.m_instance.GetCameraOffset(Player.m_localPlayer) : Vector3.zero));
            bool show = sp.z >= 0f;
            if (worldItemGo.activeSelf != show) worldItemGo.SetActive(show);
            if (!show) continue;

            worldItem.m_rt.position = sp;
        }
    }

    private void UpdateData()
    {
        if (worldItems.Count == 0) return;

        bool teleportAll = ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll);

        foreach (WorldItem? worldItem in worldItems)
        {
            if (worldItem == null) continue;
            if (!worldItem.dataDirty)
            {
                continue; // skip if never marked dirty
            }

            ItemDrop? drop = worldItem.itemDrop;
            if (drop == null) continue;
            ItemDrop.ItemData? data = drop.m_itemData;
            ItemDrop.ItemData.SharedData? shared = data.m_shared;

            // icon
            Sprite? icon = data.GetIcon();
            if (worldItem.lastIcon != icon)
            {
                worldItem.m_icon.sprite = icon;
                worldItem.lastIcon = icon;
            }

            // amount
            if (shared.m_maxStackSize > 1)
            {
                int st = data.m_stack;
                if (worldItem.lastStack != st)
                {
                    worldItem.m_amount.text = st.ToString();
                    worldItem.lastStack = st;
                }

                worldItem.m_amount.enabled = true;
            }
            else if (worldItem.m_amount.enabled)
            {
                worldItem.m_amount.enabled = false;
            }

            // quality
            if (shared.m_maxQuality > 1)
            {
                int q = data.m_quality;
                if (worldItem.lastQuality != q)
                {
                    worldItem.m_quality.text = q.ToString();
                    worldItem.lastQuality = q;
                }

                worldItem.m_quality.enabled = true;
            }
            else if (worldItem.m_quality.enabled)
            {
                worldItem.m_quality.enabled = false;
            }

            // durability
            float pct = data.GetDurabilityPercentage();
            bool useD = shared is { m_useDurability: true, m_maxDurability: > 0 } && pct < 1f;
            if (worldItem.m_durability.gameObject.activeSelf != useD)
                worldItem.m_durability.gameObject.SetActive(useD);

            if (useD && !Mathf.Approximately(worldItem.lastDurPct, pct))
            {
                worldItem.m_durability.SetValue(pct);
                worldItem.lastDurPct = pct;
            }

            // no-teleport
            bool showNoTp = !shared.m_teleportable && !teleportAll;
            if (worldItem.m_noteleport.enabled != showNoTp)
                worldItem.m_noteleport.enabled = showNoTp;

            // food
            bool isFood = shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable;
            bool hasFood = shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f;
            bool showFood = isFood && hasFood;
            if (worldItem.m_food.enabled != showFood)
                worldItem.m_food.enabled = showFood;

            if (showFood)
            {
                Color c;
                if (shared.m_food < shared.m_foodEitr / 2f && shared.m_foodStamina < shared.m_foodEitr / 2f)
                    c = m_foodEitrColor;
                else if (shared.m_foodStamina < shared.m_food / 2f)
                    c = m_foodHealthColor;
                else if (shared.m_food < shared.m_foodStamina / 2f)
                    c = m_foodStaminaColor;
                else
                    c = Color.white;

                if (worldItem.lastFoodColor != c)
                {
                    worldItem.m_food.color = c;
                    worldItem.lastFoodColor = c;
                }
            }

            // equip icon (rarely changes)
            bool showEq = data.m_equipped;
            if (worldItem.m_equiped.enabled != showEq)
                worldItem.m_equiped.enabled = showEq;

            worldItem.dataDirty = false; // reset
        }
    }

    public void UpdatePositionInterval(float value)
    {
        positionInterval = value;
    }

    public void UpdateDataInterval(float value)
    {
        dataInterval = value;
    }

    public void UpdateMaxDisplayDistance(float value)
    {
        maxDisplayDistance = value;
        maxDistSqr = maxDisplayDistance * maxDisplayDistance;
    }

    public void UpdateWorldOffset(Vector3 value)
    {
        worldOffset = value;
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

[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Start))]
static class ItemDropDisplayRegisterPatch
{
    static void Postfix(ItemDrop __instance)
    {
        ItemDropDisplayManager.Instance?.Register(__instance);
    }
}

[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.OnDestroy))]
static class ItemDropDisplayUnregisterPatch
{
    static void Prefix(ItemDrop __instance)
    {
        ItemDropDisplayManager.Instance?.Unregister(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.Awake))]
static class CloneInventoryElementInventoryGridAwakePatch
{
    [HarmonyPriority(Priority.Last)]
    static void Postfix(InventoryGrid __instance)
    {
        // instantiate one copy of the element prefab as our “DataDisplay” template
        GameObject? prefab = __instance.m_elementPrefab;
        Transform? parent = Tutorial.instance.m_windowRoot.parent; // same canvas root
        GameObject template = Object.Instantiate(prefab, parent);
        template.name = "DataDisplayTemplate";

        Object.Destroy(template.transform.Find("queued").gameObject);
        Object.Destroy(template.transform.Find("selected").gameObject);
        Object.Destroy(template.transform.Find("binding").gameObject);

        if (Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.jewelcrafting"))
        {
            Object.Destroy(template.transform.Find("JC_ItemBackground").gameObject);
        }


        template.SetActive(false);


        // If data display already exists, do nothing, otherwise add component
        if (Tutorial.instance.gameObject.GetComponent<ItemDropDisplayManager>() == null)
        {
            Tutorial.instance.gameObject.AddComponent<ItemDropDisplayManager>();
        }
    }
}