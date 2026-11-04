using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework;
using Framework.Ability;
using Framework.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 게임 진입점. 순서:
///  1) 기반 매니저(AssetLoader, Pool) 등록
///  2) GameTables 비동기 로드 → Tables.Bind → 태그 계층 / Execution / Cue 등록 → Unity Localization 초기화 (GameLocalization)
///  3) 게임 매니저 등록 (입력, 액터, 낮밤, 스폰, 건축, 퀘스트, 치트, UI)
///  4) UI 프리팹 로드(Addressables) → 타이틀 표시 + 에셋 프리로드 + 월드 스폰 (비동기, 로딩 표시)
///  5) "싱글 플레이" → 플레이어 생성 → HUD
/// 매니저 업데이트는 이 컴포넌트의 Update 한 곳에서 순서대로 호출한다.
/// </summary>
public class GameBootstrap : BaseBehaviour
{
    public AssetReferenceT<GameTables> TablesReference;
    public SceneContext Context;

    private AsyncOperationHandle<GameTables> TablesHandle;
    private bool Ready;
    private bool InGame;
    private TitleWindow Title;
    private HudWindow Hud;
    private MinimapWindow Minimap;
    private BalanceOverlayWindow Balance;

    private void Start()
    {
        startAsync().Forget();
    }

    private async UniTaskVoid startAsync()
    {
        Managers.Clear();
        Managers.Regist(new AssetLoader());
        Managers.Regist(new GameObjectPool());

        TablesHandle = TablesReference.LoadAssetAsync<GameTables>();
        var tables = await TablesHandle.Task.AsUniTask();
        if (this == null)
        {
            return;
        }

        if (tables == null)
        {
            Log.Error("GameBootstrap: GameTables load failed. Run 'Game/Setup/1. Import CSV' and 'Game/Setup/3. Addressables' first.");
            return;
        }

        Tables.Bind(tables);
        setupAbilitySystem();
        // Unity Localization: 현재 언어 문자열 테이블이 준비된 뒤에 UI 를 만든다 (Tables.Text 가 바로 번역문을 돌려주도록).
        await GameLocalization.InitializeAsync(tables.TextTable);
        if (this == null)
        {
            return;
        }


        Managers.Regist(new GameInput());
        Managers.Regist(new ActorRegistry());
        Managers.Regist(new DayNightManager());
        Managers.Regist(new SpawnManager());
        Managers.Regist(new BuildManager());
        Managers.Regist(new FoliageManager());
        Managers.Regist(new QuestManager());
        Managers.Regist(new QuestNavigator());
        Managers.Regist(new TargetSelector());
        Managers.Regist(new CheatManager());
        await setupUIAsync();
        if (this == null || Title == null)
        {
            return;
        }

        Title.SetLoading(true);
        Ready = true;

        await preloadAssets();
        if (this == null)
        {
            return;
        }

        await Managers.Get<SpawnManager>().SpawnWorldAsync();
        if (this == null)
        {
            return;
        }

        Title.SetLoading(false);
    }

    private void setupAbilitySystem()
    {
        GameplayTagManager.Clear();
        var tags = Tables.Root.Tag.All;
        for (int i = 0; i < tags.Count; ++i)
        {
            GameplayTagManager.SetParent((int)tags[i].Index, (int)tags[i].Parent);
        }

        EffectLibrary.Clear();
        GameExecutions.RegisterAll();
        GameplayCueManager.Clear();
        CueSpawner.RegisterAll();
        CombatLog.Clear();
        InteractableRegistry.Clear();
        MapMarkers.Clear();
    }

    /// <summary>
    /// UI: 레이어 캔버스 · 창 · 월드 레이어 프리팹을 Addressables 로 로드해 Instantiate 한다 (EUIAsset = AssetTable Index).
    /// 프리팹은 Game > Setup > 3b. Build UI Prefabs 가 만든다 (Documentation/VRising/UI.md).
    /// </summary>
    private async UniTask setupUIAsync()
    {
        if (EventSystem.current == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
        }

        var ui = Managers.Regist(new UIManager());
        var canvas = await loadUIPrefab(EUIAsset.LayerCanvas);
        if (canvas == null)
        {
            Log.Error("GameBootstrap: UI prefabs missing. Run 'Game/Setup/3b. Build UI Prefabs' and 'Game/Setup/4. Addressables'.");
            return;
        }

        // 레이어(World/Hud/Window/Popup/Toast/System)마다 캔버스 하나. 각 UI 가 GetLayer() 로 자기 레이어를 알려 준다.
        ui.RegistLayers(canvas);

        var worldCamera = Context != null && Context.Camera != null ? Context.Camera.GetComponent<Camera>() : Camera.main;
        var healthBars = ui.Spawn<WorldHealthBarLayer>(await loadUIPrefab(EUIAsset.WorldHealthBarLayer));
        if (healthBars != null)
        {
            healthBars.Setup(worldCamera);
        }

        var damageNumbers = ui.Spawn<DamageNumberLayer>(await loadUIPrefab(EUIAsset.DamageNumberLayer));
        if (damageNumbers != null)
        {
            damageNumbers.Setup(worldCamera);
        }

        Hud = ui.Create<HudWindow>(await loadUIPrefab(EUIAsset.HudWindow));
        ui.Create<InventoryWindow>(await loadUIPrefab(EUIAsset.InventoryWindow));
        ui.Create<CraftWindow>(await loadUIPrefab(EUIAsset.CraftWindow));
        ui.Create<BuildWindow>(await loadUIPrefab(EUIAsset.BuildWindow));
        ui.Create<ShopWindow>(await loadUIPrefab(EUIAsset.ShopWindow));
        ui.Create<WorldMapWindow>(await loadUIPrefab(EUIAsset.WorldMapWindow));
        Minimap = ui.Create<MinimapWindow>(await loadUIPrefab(EUIAsset.MinimapWindow));
        Balance = ui.Create<BalanceOverlayWindow>(await loadUIPrefab(EUIAsset.BalanceOverlayWindow));
        ui.Create<ToastWindow>(await loadUIPrefab(EUIAsset.ToastWindow), true);
        ui.Create<CheatConsoleWindow>(await loadUIPrefab(EUIAsset.CheatConsoleWindow));
        Title = ui.Create<TitleWindow>(await loadUIPrefab(EUIAsset.TitleWindow), true);
        if (Title != null)
        {
            Title.OnStartSinglePlayer = startSinglePlayer;
        }
    }

    private UniTask<GameObject> loadUIPrefab(EUIAsset InAsset)
    {
        return Managers.Get<AssetLoader>().LoadAsync<GameObject>(Tables.Address((int)InAsset), this);
    }

    /// <summary>AssetTable 의 모든 에셋을 비동기 로드해 캐시에 올린다. 이후 LoadSync 는 즉시 반환된다.</summary>
    private async UniTask preloadAssets()
    {
        var loader = Managers.Get<AssetLoader>();
        var rows = Tables.Root.Asset.All;
        var tasks = new List<UniTask>(rows.Count);
        for (int i = 0; i < rows.Count; ++i)
        {
            // 에셋 종류별 로드 타입은 AssetPreloader 하위 클래스가 안다 (AssetPreloaders.cs).
            var preloader = KindRegistry<AssetPreloader>.Get((int)rows[i].Kind);
            if (preloader != null)
            {
                tasks.Add(preloader.Preload(loader, rows[i].Address, this));
            }
        }

        await UniTask.WhenAll(tasks);
    }

    private void startSinglePlayer()
    {
        if (InGame == true)
        {
            return;
        }

        CharacterRow row;
        if (Tables.Root.Character.TryGet(Tables.ConstInt(EConst.PlayerCharacterId), out row) == false)
        {
            return;
        }

        // 프리로드가 끝난 뒤에만 버튼이 활성화되므로 동기 로드는 캐시 히트다.
        var prefab = Managers.Get<AssetLoader>().LoadSync<GameObject>(Tables.Address(row.Prefab));
        if (prefab == null)
        {
            Log.Error("Player prefab missing");
            return;
        }

        InGame = true;
        Title.Close();

        Vector3 start = Managers.Get<SpawnManager>().GetPlayerStart() + Vector3.up * 0.2f;
        var instance = Instantiate(prefab, start, Quaternion.identity);
        var player = instance.GetComponent<PlayerCharacter>();
        player.InitializePlayer(row, 0);

        var camera = Context.Camera;
        camera.Setup(player.transform);
        instance.GetComponent<PlayerController>().Initialize(player, camera);
        Hud.Open();
        Minimap.Open();
    }

    private void Update()
    {
        if (Ready == false)
        {
            return;
        }

        syncInputMaps();
        Managers.Update(Time.deltaTime);
        if (InGame == true)
        {
            handleHotkeys();
        }
    }

    /// <summary>
    /// 입력을 막는 창(BlocksGameInput)이 하나라도 열려 있거나 콘솔에 입력 중이면 Combat 맵(어빌리티 · 상호작용 · 카메라 줌/회전)을 끈다.
    /// 매니저 업데이트 전에 맞춰, 창이 열린 프레임부터 휠 줌 · 공격이 반응하지 않게 한다. 이동(Movement)은 창이 열려 있어도 된다.
    /// </summary>
    private void syncInputMaps()
    {
        var input = Managers.Get<GameInput>();
        var ui = Managers.Get<UIManager>();
        bool blocked = InGame == false || input.TextInputFocused == true || (ui != null && ui.IsAnyBlockingWindowOpen() == true);
        input.SetMapEnabled(EInputMap.Combat, blocked == false);
    }

    private void handleHotkeys()
    {
        var input = Managers.Get<GameInput>();
        var ui = Managers.Get<UIManager>();

        if (input.PressedAlways(EInputAction.Cheat) == true)
        {
            ui.Get<CheatConsoleWindow>().Toggle();
            return;
        }

        if (input.PressedAlways(EInputAction.Cancel) == true)
        {
            ui.Get<CheatConsoleWindow>().Close();
            ui.CloseAll();
            return;
        }

        if (input.Pressed(EInputAction.Inventory) == true)
        {
            ui.Get<InventoryWindow>().Toggle();
        }
        else if (input.Pressed(EInputAction.Craft) == true)
        {
            ui.Get<CraftWindow>().Toggle();
        }
        else if (input.Pressed(EInputAction.Build) == true)
        {
            Managers.Get<BuildManager>().Cancel();
            ui.Get<BuildWindow>().Toggle();
        }
        else if (input.Pressed(EInputAction.Shop) == true)
        {
            ui.Get<ShopWindow>().Toggle();
        }
        else if (input.Pressed(EInputAction.WorldMap) == true)
        {
            ui.Get<WorldMapWindow>().Toggle();
        }
        else if (input.Pressed(EInputAction.BalanceOverlay) == true)
        {
            Balance.Toggle();
        }
    }

    protected override void OnUnityDestroy()
    {
        Managers.Clear();
        GameLocalization.Shutdown();
        Tables.Bind(null);
        if (TablesHandle.IsValid() == true)
        {
            Addressables.Release(TablesHandle);
        }

        base.OnUnityDestroy();
    }
}
