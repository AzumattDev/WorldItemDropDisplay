using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static WorldItemDropDisplay.WorldItemDropDisplayPlugin;

namespace WorldItemDropDisplay;

[RequireComponent(typeof(ItemDrop))]
public class DataDisplayBehaviour : MonoBehaviour
{
    private ItemDrop _drop = null!;
    private GameObject _ui = null!;
    private RectTransform _rt = null!;
    private Image _bkg = null!, _icon = null!, _equipped = null!, _noteleport = null!, _food = null!;
    private TMP_Text _amountText = null!, _qualityText = null!, _bindingText = null!;
    private GuiBar _durabilityBar = null!;

    // last-seen values so RefreshData only repaints on real change
    private int _lastStack = -1;
    private int _lastQuality = -1;
    private string _lastName = "";
    private float _lastDurPct = -1f;
    private Sprite _lastIcon = null!;
    private Color _lastFoodColor;

    private void Awake()
    {
        _drop = GetComponent<ItemDrop>();
        ItemDropDisplayManager mgr = ItemDropDisplayManager.Instance;

        // — grab a pooled UI
        _ui = mgr.Pool.Get();
        _ui.name = "DataDisplay_" + _drop.GetInstanceID();
        Transform t = _ui.transform;
        _rt = _ui.GetComponent<RectTransform>();
        _bkg = _ui.GetComponent<Image>();
        _icon = t.Find("icon").GetComponent<Image>();
        _amountText = t.Find("amount").GetComponent<TMP_Text>();
        _qualityText = t.Find("quality").GetComponent<TMP_Text>();
        _bindingText = t.Find("binding").GetComponent<TMP_Text>();
        _equipped = t.Find("equiped").GetComponent<Image>();
        _noteleport = t.Find("noteleport").GetComponent<Image>();
        _food = t.Find("foodicon").GetComponent<Image>();
        _durabilityBar = t.Find("durability").GetComponent<GuiBar>();

        // — initial background config
        _bkg.enabled = ShowUIBackground.Value.IsOn();

        // — register for per-frame position updates
        mgr.RegisterBehaviour(this);

        // — initial paint
        RefreshData();
        StackPatch.OnStackChanged += OnStackChanged;
        ItemDropDisplayManager.Instance.SettingsChanged += OnConfigChanged;
    }

    private void OnDestroy()
    {
        StackPatch.OnStackChanged -= OnStackChanged;
        ItemDropDisplayManager.Instance.SettingsChanged -= OnConfigChanged;
        if (_ui == null || ItemDropDisplayManager.Instance == null) return;
        ItemDropDisplayManager.Instance.UnregisterBehaviour(this);
        ItemDropDisplayManager.Instance.Pool.Release(_ui);
    }

    private void OnStackChanged(ItemDrop d)
    {
        if (d == _drop) RefreshData();
    }

    private void OnConfigChanged()
    {
        // background toggle may have changed
        _bkg.enabled = ShowUIBackground.Value.IsOn();
        RefreshData();
    }

    /// <summary>
    /// Only updates the bits that actually changed.
    /// </summary>
    private void RefreshData()
    {
        ItemDrop.ItemData? data = _drop.m_itemData;
        ItemDrop.ItemData.SharedData? shared = data.m_shared;

        // — ICON
        Sprite? sp = data.GetIcon();
        if (_lastIcon != sp)
        {
            _icon.sprite = sp;
            _lastIcon = sp;
        }

        // — AMOUNT
        if (ShowAmount.Value.IsOn() && shared.m_maxStackSize > 1)
        {
            int st = data.m_stack;
            if (_lastStack != st)
            {
                _amountText.text = st.ToString();
                _lastStack = st;
            }

            _amountText.enabled = true;
        }
        else
        {
            _amountText.enabled = false;
        }

        // — QUALITY
        if (ShowQuality.Value.IsOn() && shared.m_maxQuality > 1)
        {
            int q = data.m_quality;
            if (_lastQuality != q)
            {
                _qualityText.text = q.ToString();
                _lastQuality = q;
            }

            _qualityText.enabled = true;
        }
        else
        {
            _qualityText.enabled = false;
        }

        // — Item Name
        if (ShowName.Value.IsOn() && !string.IsNullOrWhiteSpace(shared.m_name))
        {
            string sharedMName = shared.m_name;
            if (_lastName != sharedMName)
            {
                _bindingText.text = Localization.instance.Localize(sharedMName);
                _lastName = sharedMName;
            }

            _bindingText.enabled = true;
        }
        else
        {
            _bindingText.enabled = false;
        }

        // — DURABILITY
        float pct = data.GetDurabilityPercentage();
        bool useD = ShowDurability.Value.IsOn() && shared.m_useDurability && shared.m_maxDurability > 0 && pct < 1f;
        _durabilityBar.gameObject.SetActive(useD);
        if (useD && !Mathf.Approximately(_lastDurPct, pct))
        {
            _durabilityBar.SetValue(pct);
            _lastDurPct = pct;
        }

        // — NO-TELEPORT
        bool noTp = ShowNoTeleport.Value.IsOn() && !shared.m_teleportable && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll);
        _noteleport.enabled = noTp;

        // — FOOD ICON
        bool showF = ShowFoodIcon.Value.IsOn() && shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable && (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f);
        _food.enabled = showF;
        if (showF)
        {
            Color c;
            if (shared.m_food < shared.m_foodEitr / 2f && shared.m_foodStamina < shared.m_foodEitr / 2f)
                c = ItemDropDisplayManager.Instance.m_foodEitrColor;
            else if (shared.m_foodStamina < shared.m_food / 2f)
                c = ItemDropDisplayManager.Instance.m_foodHealthColor;
            else if (shared.m_food < shared.m_foodStamina / 2f)
                c = ItemDropDisplayManager.Instance.m_foodStaminaColor;
            else
                c = Color.white;

            if (_lastFoodColor != c)
            {
                _food.color = c;
                _lastFoodColor = c;
            }
        }

        // — EQUIPPED ICON
        _equipped.enabled = data.m_equipped;
    }

    /// <summary>
    /// Called each frame by the manager to move / show / hide this UI.
    /// </summary>
    public void UpdatePosition(Vector3 playerPos, Camera cam, Vector3 offset, float maxDistSqr, bool hideAll)
    {
        if (hideAll)
        {
            if (_ui.activeSelf) _ui.SetActive(false);
            return;
        }

        Vector3 dropPos = _drop.transform.position;

        // exclude fish if underwater
        if (_drop.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Fish)
        {
            Fish? fishComp = _drop.GetComponent<Fish>();
            if (fishComp != null && (!fishComp.IsOutOfWater() || fishComp is { m_isJumping: true, m_jumpedFromLand: false }))
            {
                if (_ui.activeSelf) _ui.SetActive(false);
                return;
            }
        }

        Vector3 dv = dropPos - playerPos;
        if (dv.sqrMagnitude > maxDistSqr)
        {
            if (_ui.activeSelf) _ui.SetActive(false);
            return;
        }

        // world → screen
        Vector3 sp = cam.WorldToScreenPointScaled(dropPos + offset);
        bool show = sp.z >= 0f;
        if (_ui.activeSelf != show) _ui.SetActive(show);
        if (!show) return;

        _rt.position = sp;
    }
}