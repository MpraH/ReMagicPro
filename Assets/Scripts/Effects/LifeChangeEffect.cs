public class LifeChangeEffect : Effect
{
    private int _lifeChange;

    LifeChangeEffect(int lifeChange) {
        _lifeChange = lifeChange;
    }

    void ResolveForTarget(dynamic target)
    {
        if (target is Player)
        {
            if (_lifeChange > 0)
            {
                GameManager.Instance.TryGainLife(target, _lifeChange, true);
            }
            else
            {
                GameManager.Instance.LoseLife(target, _lifeChange * -1, true);
            }
        }
    }
}