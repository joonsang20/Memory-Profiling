using UnityEngine;

public class BenchmarkProjectile : MonoBehaviour
{
    BenchmarkSpawner _spawner;
    Rigidbody        _rb;
    float            _lifetime;
    float            _timer;
    bool             _running;

    // Pool 모드: OnPoolCreate에서 1회 할당. Bad/Leak: InitBad마다 new.
    byte[] _dummyData;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!_running) return;

        _timer += Time.deltaTime;
        if (_timer >= _lifetime)
        {
            _running = false;
            _spawner.ReturnProjectile(this);
        }
    }

    // ── Pool 콜백 ──────────────────────────────────────────

    // ObjectPool createFunc: 첫 생성 시 1회만 호출
    public void OnPoolCreate()
    {
        _dummyData = new byte[2048];
    }

    // ObjectPool actionOnGet: 풀에서 꺼낼 때 호출
    public void OnPoolGet(BenchmarkSpawner spawner, float lifetime, Vector3 velocity)
    {
        // SetActive 먼저 → Awake가 실행되어 _rb가 캐싱된 뒤 사용
        gameObject.SetActive(true);

        _spawner  = spawner;
        _lifetime = lifetime;
        _timer    = 0f;
        _running  = true;

        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.linearVelocity  = velocity;
    }

    // ObjectPool actionOnRelease: 풀에 반납할 때 호출
    public void OnPoolRelease()
    {
        _running = false;
        gameObject.SetActive(false);
    }

    // ── Bad / Leak 모드 ────────────────────────────────────

    // Instantiate 직후 호출. 매번 new byte[2048] 할당.
    public void InitBad(BenchmarkSpawner spawner, float lifetime, Vector3 velocity)
    {
        _spawner   = spawner;
        _lifetime  = lifetime;
        _timer     = 0f;
        _running   = true;
        _dummyData = new byte[2048];

        _rb.linearVelocity = velocity;
    }

    // ClearActive에서 즉시 수명 종료 시 호출 (ReturnProjectile 콜백 방지)
    public void ForceStop()
    {
        _running = false;
    }
}
