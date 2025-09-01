using System.Collections;
using TMPro;
using Unity.Burst;
using UnityEngine;

public class VisualEffectManager : MonoBehaviour
{
    public static VisualEffectManager Instance;

    [SerializeField] private GameObject _playerLifeContainer;
    [SerializeField] private GameObject _enemyLifeContainer;
    [SerializeField] private GameObject _floatingDamagePrefab;

    [SerializeField] private GameObject _favouritePopupPrefab;

    // Tracks cumulative life changes during a combat step
    private TMP_Text _playerLifeDeltaText;
    private TMP_Text _enemyLifeDeltaText;
    private int _playerLifeDelta = 0;
    private int _enemyLifeDelta = 0;
    private Coroutine _playerDeltaRoutine;
    private Coroutine _enemyDeltaRoutine;
    // When true, life delta text will not automatically fade out.
    private bool _lifeDeltaFadeDeferred = false;

    void Awake()
    {
        Instance = this;
    }

    public void ShowFloatingDamage(int amount, GameObject target)
    {
        if (target == _playerLifeContainer || target == _enemyLifeContainer)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.dealDamage);
            UpdateLifeDelta(target, -amount);
            return;
        }

        if (_floatingDamagePrefab == null)
        {
            Debug.LogError("Missing floatingDamagePrefab!");
            return;
        }

        GameObject obj = Instantiate(_floatingDamagePrefab);
        obj.transform.SetParent(GameObject.Find("Canvas").transform, false);

        RectTransform canvasRect = GameObject.Find("Canvas").GetComponent<RectTransform>();
        RectTransform targetRect = target.GetComponent<RectTransform>();
        RectTransform rt = obj.GetComponent<RectTransform>();

        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, targetRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, Camera.main, out Vector2 localPoint);
        rt.anchoredPosition = localPoint;

        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(100, 40);

        TMP_Text text = obj.GetComponent<TMP_Text>();
        text.fontSize = 48;
        text.enableAutoSizing = false;
        text.text = "-" + amount;
        text.color = Color.red;

        SoundManager.Instance.PlaySound(SoundManager.Instance.dealDamage);

        StartCoroutine(FadeAndFloatText(obj, target == _playerLifeContainer));
    }
    public void ShowFloatingDamageForPlayer(int amount, bool forHumanPlayer)
    {
        ShowFloatingDamage(amount, forHumanPlayer ? _playerLifeContainer : _enemyLifeContainer);
    }
    public void ShowFloatingHeal(int amount, GameObject target)
    {
        Debug.Log($"ShowFloatingHeal called: amount={amount}, target={target.name}");

        if (target == _playerLifeContainer || target == _enemyLifeContainer)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.gain_life);
            UpdateLifeDelta(target, amount);
            return;
        }

        if (_floatingDamagePrefab == null)
        {
            Debug.LogError("Missing floatingDamagePrefab!");
            return;
        }

        GameObject obj = Instantiate(_floatingDamagePrefab);
        obj.transform.SetParent(GameObject.Find("Canvas").transform, false);

        RectTransform canvasRect = GameObject.Find("Canvas").GetComponent<RectTransform>();
        RectTransform targetRect = target.GetComponent<RectTransform>();
        RectTransform rt = obj.GetComponent<RectTransform>();

        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, targetRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, Camera.main, out Vector2 localPoint);
        rt.anchoredPosition = localPoint;

        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(100, 40);

        TMP_Text text = obj.GetComponent<TMP_Text>();
        text.fontSize = 48;
        text.enableAutoSizing = false;
        text.text = "+" + amount;
        text.color = Color.green;

        SoundManager.Instance.PlaySound(SoundManager.Instance.gain_life); // use appropriate sound

        StartCoroutine(FadeAndFloatText(obj, target == _playerLifeContainer));
    }
    public void ShowFloatingHealForPlayer(int amount, bool forHumanPlayer)
    {
        ShowFloatingHeal(amount, forHumanPlayer ? _playerLifeContainer : _enemyLifeContainer);
    }

    public IEnumerator FadeAndFloatText(GameObject obj, bool floatUp)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        TMP_Text text = obj.GetComponent<TMP_Text>();
        Vector3 startPos = rt.localPosition;
        float t = 0f;
        float direction = floatUp ? 1f : -1f;

        Color baseColor = text.color;

        while (t < 1.25f)
        {
            t += Time.deltaTime;
            rt.localPosition = startPos + new Vector3(0, t * 20f * direction, 0);
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1 - t * 0.8f);
            yield return null;
        }

        Destroy(obj);
        yield break;
    }
    public void DeferLifeDeltaFade(bool defer)
    {
        _lifeDeltaFadeDeferred = defer;
        if (defer)
        {
            if (_playerDeltaRoutine != null)
            {
                StopCoroutine(_playerDeltaRoutine);
                _playerDeltaRoutine = null;
            }
            if (_enemyDeltaRoutine != null)
            {
                StopCoroutine(_enemyDeltaRoutine);
                _enemyDeltaRoutine = null;
            }
        }
    }
    private void UpdateLifeDelta(GameObject target, int change)
    {
        bool isPlayer = target == _playerLifeContainer;
        bool isEnemy = target == _enemyLifeContainer;
        if (!isPlayer && !isEnemy)
            return;

        TMP_Text txt = isPlayer ? _playerLifeDeltaText : _enemyLifeDeltaText;
        int total = isPlayer ? _playerLifeDelta : _enemyLifeDelta;
        total += change;

        if (txt == null)
        {
            if (_floatingDamagePrefab == null)
            {
                Debug.LogError("Missing floatingDamagePrefab!");
                return;
            }

            GameObject obj = Instantiate(_floatingDamagePrefab);
            obj.transform.SetParent(GameObject.Find("Canvas").transform, false);

            RectTransform canvasRect = GameObject.Find("Canvas").GetComponent<RectTransform>();
            RectTransform targetRect = target.GetComponent<RectTransform>();
            RectTransform rt = obj.GetComponent<RectTransform>();

            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, targetRect.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, Camera.main, out Vector2 localPoint);
            rt.anchoredPosition = localPoint;

            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(100, 40);

            txt = obj.GetComponent<TMP_Text>();

            if (isPlayer)
                _playerLifeDeltaText = txt;
            else
                _enemyLifeDeltaText = txt;
        }

        txt.fontSize = 48;
        txt.enableAutoSizing = false;
        txt.text = (total > 0 ? "+" : "") + total;
        txt.color = total > 0 ? Color.green : Color.red;

        if (isPlayer)
        {
            _playerLifeDelta = total;
            if (!_lifeDeltaFadeDeferred)
            {
                if (_playerDeltaRoutine != null)
                    StopCoroutine(_playerDeltaRoutine);
                _playerDeltaRoutine = StartCoroutine(DelayFinalize(target));
            }
        }
        else
        {
            _enemyLifeDelta = total;
            if (!_lifeDeltaFadeDeferred)
            {
                if (_enemyDeltaRoutine != null)
                    StopCoroutine(_enemyDeltaRoutine);
                _enemyDeltaRoutine = StartCoroutine(DelayFinalize(target));
            }
        }
    }
    public void ResetLifeDeltas()
    {
        if (_playerLifeDeltaText != null)
        {
            Destroy(_playerLifeDeltaText.gameObject);
            _playerLifeDeltaText = null;
        }
        if (_enemyLifeDeltaText != null)
        {
            Destroy(_enemyLifeDeltaText.gameObject);
            _enemyLifeDeltaText = null;
        }
        _playerLifeDelta = 0;
        _enemyLifeDelta = 0;
    }
    public void FinalizeLifeDeltas()
    {
        _lifeDeltaFadeDeferred = false;
        if (_playerLifeDeltaText != null)
            StartCoroutine(VisualEffectManager.Instance.FadeAndFloatText(_playerLifeDeltaText.gameObject, true));
        if (_enemyLifeDeltaText != null)
            StartCoroutine(VisualEffectManager.Instance.FadeAndFloatText(_enemyLifeDeltaText.gameObject, false));

        _playerLifeDeltaText = null;
        _enemyLifeDeltaText = null;
        if (_playerDeltaRoutine != null)
        {
            StopCoroutine(_playerDeltaRoutine);
            _playerDeltaRoutine = null;
        }
        if (_enemyDeltaRoutine != null)
        {
            StopCoroutine(_enemyDeltaRoutine);
            _enemyDeltaRoutine = null;
        }
        _playerLifeDelta = 0;
        _enemyLifeDelta = 0;
    }
    private IEnumerator DelayFinalize(GameObject target)
    {
        yield return new WaitForSeconds(1.5f);
        if (target == _playerLifeContainer && _playerLifeDeltaText != null)
        {
            StartCoroutine(VisualEffectManager.Instance.FadeAndFloatText(_playerLifeDeltaText.gameObject, true));
            _playerLifeDeltaText = null;
            _playerLifeDelta = 0;
            _playerDeltaRoutine = null;
        }
        else if (target == _enemyLifeContainer && _enemyLifeDeltaText != null)
        {
            StartCoroutine(VisualEffectManager.Instance.FadeAndFloatText(_enemyLifeDeltaText.gameObject, false));
            _enemyLifeDeltaText = null;
            _enemyLifeDelta = 0;
            _enemyDeltaRoutine = null;
        }
    }

    public void ShowFavouritePopup()
    {
        if (_favouritePopupPrefab == null || _playerLifeContainer == null)
            return;

        GameObject obj = Instantiate(_favouritePopupPrefab);
        obj.transform.SetParent(GameObject.Find("Canvas").transform, false);

        RectTransform canvasRect = GameObject.Find("Canvas").GetComponent<RectTransform>();
        RectTransform targetRect = _playerLifeContainer.GetComponent<RectTransform>();
        RectTransform rt = obj.GetComponent<RectTransform>();

        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, targetRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, Camera.main, out Vector2 localPoint);
        rt.anchoredPosition = localPoint;

        obj.AddComponent<FavouritePopupVFX>();
    }

    public IEnumerator MoveCardToLifeContainer(Transform tf, Vector3 start, bool toHumanPlayer, float duration)
    {
        GameObject lifeContainer = toHumanPlayer ? _playerLifeContainer : _enemyLifeContainer;
        return MoveCard(tf, start, lifeContainer.transform.position, duration);
    }

    public IEnumerator MoveCard(Transform tf, Vector3 start, Vector3 end, float duration)
    {
        Canvas cardCanvas = tf.GetComponentInChildren<Canvas>();
        int originalOrder = 0;
        bool originalOverride = false;
        if (cardCanvas != null)
        {
            originalOrder = cardCanvas.sortingOrder;
            originalOverride = cardCanvas.overrideSorting;
            cardCanvas.overrideSorting = true;
            cardCanvas.sortingOrder = 100;
        }

        float t = 0f;
        while (t < duration)
        {
            if (tf == null) yield break;
            t += Time.deltaTime;
            tf.position = Vector3.Lerp(start, end, t / duration);
            yield return null;
        }

        if (tf != null)
            tf.position = end;

        if (cardCanvas != null)
        {
            cardCanvas.sortingOrder = originalOrder;
            cardCanvas.overrideSorting = originalOverride;
        }
    }

    public GameObject GetLifeContainer(bool ofPlayer)
    {
        return ofPlayer ? _playerLifeContainer : _enemyLifeContainer;
    }
}