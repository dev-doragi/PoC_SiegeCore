using SiegeCore.Projectile;
using SiegeCore.Rat;
using TMPro;
using UnityEngine;

namespace SiegeCore.UI
{
    public sealed class RatEconomyOverlay : MonoBehaviour
    {
        [SerializeField] private SiegeHealth _allySiege;
        [SerializeField] private SiegeHealth _enemySiege;
        [SerializeField] private RatDispenser _allySupply;
        [SerializeField] private RatDispenser _enemySupply;
        [SerializeField] private TMP_Text _allyText;
        [SerializeField] private TMP_Text _enemyText;
        private float _nextRefresh;

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;
            Refresh(_allyText, "ALLY", _allySiege, _allySupply);
            Refresh(_enemyText, "ENEMY", _enemySiege, _enemySupply);
        }

        private void Refresh(TMP_Text label, string side, SiegeHealth siege, RatDispenser supply)
        {
            if (label == null || siege == null || supply == null) return;
            label.text = $"{side}  SIEGE {siege.CurrentHealth:0}/{siege.MaxHealth:0}\n"
                + $"SUPPLY {supply.ProductionRatio * 100f:0.#}%   RATS {supply.Population}/{supply.PopulationCap} B";
        }
    }
}
