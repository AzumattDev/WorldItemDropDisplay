using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using static WorldItemDropDisplay.WorldItemDropDisplayPlugin;

namespace WorldItemDropDisplay;

public class ItemDropDisplayManager : MonoBehaviour
{
    public static ItemDropDisplayManager Instance { get; private set; } = null!;

    public event Action SettingsChanged = null!;

    public const string TemplateName = "DataDisplayTemplate";
    public const string HideWidd = "WIDD";

    [Tooltip("How often (seconds) to refresh positions")] [SerializeField]
    private float positionInterval = ItemPositionInterval.Value;

    [Tooltip("Maximum distance to show the display")] [SerializeField]
    private float maxDisplayDistance = ItemMaxDisplayDistance.Value;

    [Tooltip("Vertical offset above the item")] [SerializeField]
    private Vector3 worldOffset = ItemWorldOffset.Value;

    private GameObject _template = null!;
    private Transform _templateParent = null!;

    private float _maxDistSqr;
    private float _posTimer;
    private Camera _cam = null!;
    private Transform _playerTransform = null!;

    public readonly Color m_foodEitrColor = new(0.6f, 0.6f, 1f, 1f);
    public readonly Color m_foodHealthColor = new(1f, 0.5f, 0.5f, 1f);
    public readonly Color m_foodStaminaColor = new(1f, 1f, 0.5f, 1f);

    // Pool for the per-item UI GameObjects
    public IObjectPool<GameObject> Pool = null!;

    // All active DataDisplayBehaviours for per-frame position updates
    private readonly List<DataDisplayBehaviour> _behaviours = [];

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        _maxDistSqr = maxDisplayDistance * maxDisplayDistance;

        ShowUIBackground.SettingChanged += RaiseSettingsChanged;
        ShowAmount.SettingChanged += RaiseSettingsChanged;
        ShowQuality.SettingChanged += RaiseSettingsChanged;
        ShowDurability.SettingChanged += RaiseSettingsChanged;
        ShowNoTeleport.SettingChanged += RaiseSettingsChanged;
        ShowFoodIcon.SettingChanged += RaiseSettingsChanged;
        ShowName.SettingChanged += RaiseSettingsChanged;
    }

    private void Start()
    {
        _cam = Utils.GetMainCamera();
        if (_cam == null) _cam = Camera.main;
        if (_cam == null)
        {
            WorldItemDropDisplayLogger.LogError("No camera found for ItemDropDisplayManager");
            return;
        }

        // find the template we made in the patch
        _template = Tutorial.instance.m_windowRoot.parent.transform.Find(TemplateName).gameObject;
        if (_template == null)
        {
            WorldItemDropDisplayLogger.LogError("[ItemDropDisplayManager] DataDisplayTemplate not found!");
            enabled = false;
            return;
        }

        _templateParent = _template.transform.parent;

        // Build the pool using your existing template
        Pool = new ObjectPool<GameObject>(
            createFunc: CreatePooledItem,
            //actionOnGet: go => go.SetActive(true),
            actionOnRelease: go => go.SetActive(false),
            actionOnDestroy: go => Destroy(go),
            collectionCheck: false,
            defaultCapacity: 16,
            maxSize: 512
        );
    }

    private void OnDestroy()
    {
        ShowUIBackground.SettingChanged -= RaiseSettingsChanged;
        ShowAmount.SettingChanged -= RaiseSettingsChanged;
        ShowQuality.SettingChanged -= RaiseSettingsChanged;
        ShowDurability.SettingChanged -= RaiseSettingsChanged;
        ShowNoTeleport.SettingChanged -= RaiseSettingsChanged;
        ShowFoodIcon.SettingChanged -= RaiseSettingsChanged;
        ShowName.SettingChanged -= RaiseSettingsChanged;
    }


    private GameObject CreatePooledItem()
    {
        // Find the template in the same way you did before
        GameObject? go = Instantiate(_template, _templateParent);
        RectTransform? rt = go.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        go.SetActive(false);
        return go;
    }

    /// <summary>
    /// Called by each DataDisplayBehaviour in Awake().
    /// </summary>
    public void RegisterBehaviour(DataDisplayBehaviour beh) => _behaviours.Add(beh);

    /// <summary>
    /// Called by each DataDisplayBehaviour in OnDestroy().
    /// </summary>
    public void UnregisterBehaviour(DataDisplayBehaviour beh) => _behaviours.Remove(beh);

    public void UpdatePositionInterval(float v) => positionInterval = v;

    public void UpdateMaxDisplayDistance(float v)
    {
        maxDisplayDistance = v;
        _maxDistSqr = v * v;
    }

    public void UpdateWorldOffset(Vector3 v) => worldOffset = v;

    private void Update()
    {
        _posTimer += Time.unscaledDeltaTime;
        while (_posTimer >= positionInterval)
        {
            _posTimer -= positionInterval;
            UpdatePositions();
        }
    }

    private void UpdatePositions()
    {
        if (_cam == null) _cam = Utils.GetMainCamera();
        if (_playerTransform == null && Player.m_localPlayer != null)
            _playerTransform = Player.m_localPlayer.transform;
        if (_cam == null || _playerTransform == null || _behaviours.Count == 0)
            return;

        bool hideAll = InventoryGui.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible() || Hud.IsPieceSelectionVisible() || !Player.m_localPlayer!.TakeInput() || Player.m_localPlayer.m_customData.ContainsKey(HideWidd);

        Vector3 playerPos = _playerTransform.position;

        for (int i = 0, n = _behaviours.Count; i < n; ++i)
        {
            _behaviours[i].UpdatePosition(playerPos, _cam, worldOffset, _maxDistSqr, hideAll);
        }
    }

    private void RaiseSettingsChanged(object _, EventArgs __) => SettingsChanged?.Invoke();
}