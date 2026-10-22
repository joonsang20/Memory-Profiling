using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

public class BenchmarkSpawner : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [SerializeField] int   spawnRate        = 250;
    [SerializeField] float projectileLifetime = 1.5f;
    [SerializeField] float spawnAreaRadius  = 1.5f;
    [SerializeField] int   randomSeed       = 42;
    [SerializeField] int   startMode        = 1;
    [SerializeField] bool  autoStart        = false;
    [SerializeField] int   projectileLayer  = 0;

    // ── Leak 추적용 static ────────────────────────────────────────────────────
    public static List<GameObject>           LeakedObjects    = new List<GameObject>();
    public static List<BenchmarkProjectile>  LeakedComponents = new List<BenchmarkProjectile>();

    // ── Runtime 상태 ──────────────────────────────────────────────────────────
    GameObject                      _template;
    ObjectPool<BenchmarkProjectile> _pool;
    int                             _currentMode;
    bool                            _spawning;
    float                           _accumulator;
    int                             _activeCount;

    // Pool 델리게이트 - Awake에서 1회 생성, 매 프레임 재생성 금지
    Func<BenchmarkProjectile>       _poolCreate;
    Action<BenchmarkProjectile>     _poolRelease;
    Action<BenchmarkProjectile>     _poolDestroy;

    // 초당 실제 생성 수 측정
    int   _spawnedThisPeriod;
    float _rateTimer;
    float _displayedRate;

    // UI 캐싱
    bool     _uiVisible  = true;
    string   _cachedUI   = "";
    float    _uiTimer;
    GUIStyle _uiStyle;

    // ── Unity 생명주기 ────────────────────────────────────────────────────────
    void Awake()
    {
        Screen.SetResolution(1280, 720, false);

        Random.InitState(randomSeed);

        _template = BuildTemplate();

        // 델리게이트를 필드에 저장해 ObjectPool 생성 이후 재생성 없음
        _poolCreate  = CreatePooledProjectile;
        _poolRelease = p => p.OnPoolRelease();
        _poolDestroy = p => Destroy(p.gameObject);

        int prewarmCount = Mathf.CeilToInt(spawnRate * projectileLifetime);
        _pool = new ObjectPool<BenchmarkProjectile>(
            createFunc:      _poolCreate,
            actionOnGet:     null,        // Get 시 초기화는 SpawnOne에서 OnPoolGet 직접 호출
            actionOnRelease: _poolRelease,
            actionOnDestroy: _poolDestroy,
            collectionCheck: false,
            defaultCapacity: prewarmCount,
            maxSize:         prewarmCount * 2
        );
    }

    void Start()
    {
        if (Camera.main != null)
            transform.position = Camera.main.transform.position + Camera.main.transform.forward * 8f;

        SetMode(startMode);
        if (autoStart) _spawning = true;
    }

    void Update()
    {
        HandleInput();

        if (_spawning)
        {
            _accumulator += Time.deltaTime * spawnRate;
            while (_accumulator >= 1f)
            {
                SpawnOne();
                _accumulator -= 1f;
                _spawnedThisPeriod++;
            }
        }

        _rateTimer += Time.deltaTime;
        if (_rateTimer >= 1f)
        {
            _displayedRate     = _spawnedThisPeriod / _rateTimer;
            _spawnedThisPeriod = 0;
            _rateTimer         = 0f;
        }

        _uiTimer += Time.deltaTime;
        if (_uiTimer >= 0.5f)
        {
            BuildUIString();
            _uiTimer = 0f;
        }
    }

    void OnGUI()
    {
        if (!_uiVisible) return;

        if (_uiStyle == null)
        {
            _uiStyle           = new GUIStyle(GUI.skin.label);
            _uiStyle.fontSize  = 22;
            _uiStyle.normal.textColor = Color.white;
        }

        GUI.Label(new Rect(10, 10, 400, 260), _cachedUI, _uiStyle);
    }

    // ── 입력 처리 ─────────────────────────────────────────────────────────────
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetMode(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetMode(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetMode(3);
        if (Input.GetKeyDown(KeyCode.Space))  _spawning = !_spawning;
        if (Input.GetKeyDown(KeyCode.Alpha0)) PrepareSnapshot();
        if (Input.GetKeyDown(KeyCode.F1))     _uiVisible = !_uiVisible;
    }

    // ── 모드 관리 ─────────────────────────────────────────────────────────────
    void SetMode(int mode)
    {
        ClearActive();
        _currentMode = mode;
        _accumulator = 0f;

        if (_currentMode == 2)
            PrewarmPool();

        BuildUIString();
    }

    void ClearActive()
    {
        // Bad / Leak: 씬에 남아있는 투사체 전부 Destroy
        if (_currentMode == 1 || _currentMode == 3)
        {
            var projectiles = FindObjectsByType<BenchmarkProjectile>(FindObjectsSortMode.None);
            foreach (var p in projectiles)
            {
                if (p.gameObject == _template) continue;
                p.ForceStop();
                Destroy(p.gameObject);
            }
        }
        // Pool: 활성 투사체를 모두 Release
        else if (_currentMode == 2)
        {
            var projectiles = FindObjectsByType<BenchmarkProjectile>(FindObjectsSortMode.None);
            foreach (var p in projectiles)
            {
                if (p.gameObject == _template) continue;
                p.ForceStop();
                _pool.Release(p);
            }
        }

        _activeCount = 0;
    }

    // ── 스폰 ──────────────────────────────────────────────────────────────────
    void SpawnOne()
    {
        Vector3 spawnPos = transform.position + new Vector3(
            (Random.value * 2f - 1f) * spawnAreaRadius,
            (Random.value * 2f - 1f) * spawnAreaRadius,
            (Random.value * 2f - 1f) * spawnAreaRadius
        );
        Vector3 velocity = Random.onUnitSphere * 3f;

        if (_currentMode == 1)
        {
            // Bad: 매번 Instantiate + new byte[2048]
            GameObject go = Instantiate(_template, spawnPos, Random.rotation);
            go.layer = projectileLayer;
            go.SetActive(true);
            var p = go.GetComponent<BenchmarkProjectile>();
            p.InitBad(this, projectileLifetime, velocity);
            _activeCount++;
        }
        else if (_currentMode == 2)
        {
            // Pool: Get → OnPoolGet으로 초기화
            var p = _pool.Get();
            p.transform.SetPositionAndRotation(spawnPos, Random.rotation);
            p.gameObject.layer = projectileLayer;
            p.OnPoolGet(this, projectileLifetime, velocity);
            _activeCount++;
        }
        else if (_currentMode == 3)
        {
            // Leak: Instantiate + Destroy하되 static 리스트에 참조 보관
            GameObject go = Instantiate(_template, spawnPos, Random.rotation);
            go.layer = projectileLayer;
            go.SetActive(true);
            var p = go.GetComponent<BenchmarkProjectile>();
            p.InitBad(this, projectileLifetime, velocity);
            LeakedObjects.Add(go);
            LeakedComponents.Add(p);
            _activeCount++;
        }
    }

    // 투사체가 수명 종료 시 직접 호출 (delegate 재생성 없이 메서드 직접 참조)
    public void ReturnProjectile(BenchmarkProjectile p)
    {
        _activeCount--;

        if (_currentMode == 1)
        {
            Destroy(p.gameObject);
        }
        else if (_currentMode == 2)
        {
            _pool.Release(p);
        }
        else if (_currentMode == 3)
        {
            // LeakedList는 비우지 않음 - C# managed shell 유지가 목적
            Destroy(p.gameObject);
        }
    }

    // ── Pool ──────────────────────────────────────────────────────────────────
    BenchmarkProjectile CreatePooledProjectile()
    {
        GameObject go = Instantiate(_template, Vector3.zero, Quaternion.identity);
        go.layer = projectileLayer;
        go.SetActive(false);
        var p = go.GetComponent<BenchmarkProjectile>();
        p.OnPoolCreate();   // byte[2048] 1회 할당
        return p;
    }

    void PrewarmPool()
    {
        int count = Mathf.CeilToInt(spawnRate * projectileLifetime);
        var temp  = new BenchmarkProjectile[count];
        for (int i = 0; i < count; i++)
            temp[i] = _pool.Get();
        for (int i = 0; i < count; i++)
            _pool.Release(temp[i]);
    }

    // ── 스냅샷 준비 (키 0) ───────────────────────────────────────────────────
    void PrepareSnapshot()
    {
        _spawning = false;
        ClearActive();
        LeakedObjects.Clear();
        LeakedComponents.Clear();
        GC.Collect();
        BuildUIString();
    }

    // ── 템플릿 생성 ───────────────────────────────────────────────────────────
    GameObject BuildTemplate()
    {
        // CreatePrimitive가 만든 기본 머티리얼을 복사해 사용 (빌드에서 셰이더 누락 방지)
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.localScale = Vector3.one * 0.3f;

        var originalMat = sphere.GetComponent<Renderer>().sharedMaterial;
        var mat         = new Material(originalMat);
        mat.color       = Color.cyan;
        sphere.GetComponent<Renderer>().material = mat;

        var rb = sphere.AddComponent<Rigidbody>();
        rb.useGravity    = false;
        rb.linearDamping = 0.5f;

        sphere.AddComponent<BenchmarkProjectile>();

        sphere.layer = projectileLayer;
        sphere.SetActive(false);
        sphere.name = "[ProjectileTemplate]";
        DontDestroyOnLoad(sphere);
        return sphere;
    }

    // ── UI ────────────────────────────────────────────────────────────────────
    void BuildUIString()
    {
        string modeName = _currentMode switch
        {
            1 => "Bad (Instantiate/Destroy)",
            2 => "Pool (ObjectPool)",
            3 => "Leak (Static Reference)",
            _ => "None"
        };

        _cachedUI = string.Format(
            "Mode : {0}\nActive: {1}\nSpawn : {2:F0}/s\nLeaked: {3}\n\n[1] Bad  [2] Pool  [3] Leak\n[Space] Start/Stop  [0] Snapshot\n[F1] Toggle UI",
            modeName,
            _activeCount,
            _displayedRate,
            LeakedObjects.Count
        );
    }
}
