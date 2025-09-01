using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class Player
{
    public List<Card> Deck = new List<Card>();
    public List<Card> Hand = new List<Card>();
    public List<Card> Battlefield = new List<Card>();
    public List<Card> Graveyard = new List<Card>();
    public List<Card> StartingPermanents = new List<Card>();
    public bool hasPlayedLandThisTurn = false;
    public ManaPool ColoredMana = new ManaPool();
    public int Life = 20;

    // Number of additional turns queued for this player
    public int extraTurns = 0;

    [System.Serializable]
    public class ManaPool
    {
        public int White = 0;
        public int Blue = 0;
        public int Black = 0;
        public int Red = 0;
        public int Green = 0;
        public int Colorless = 0;

        public void Clear()
        {
            White = Blue = Black = Red = Green = Colorless = 0;
        }

        public int Total()
        {
            return White + Blue + Black + Red + Green + Colorless;
        }

        public bool CanPay(Dictionary<string, int> cost)
        {
            int genericRequired = cost.ContainsKey("Colorless") ? cost["Colorless"] : 0;

            // First check for specific colors
            foreach (var kvp in cost)
            {
                string color = kvp.Key;
                int required = kvp.Value;

                if (color == "Colorless") continue;  // skip for now

                int available = color switch
                {
                    "White" => White,
                    "Blue" => Blue,
                    "Black" => Black,
                    "Red" => Red,
                    "Green" => Green,
                    _ => 0
                };

                if (available < required)
                    return false;
            }

            // Now check if total leftover can pay the generic cost
            int leftover = Total();

            // Subtract colored requirements
            foreach (var kvp in cost)
            {
                if (kvp.Key != "Colorless")
                    leftover -= kvp.Value;
            }

            return leftover >= genericRequired;
        }

        public void Pay(Dictionary<string, int> cost)
        {
            int genericToSpend = cost.ContainsKey("Colorless") ? cost["Colorless"] : 0;

            // Pay specific colors first
            foreach (var kvp in cost)
            {
                string color = kvp.Key;
                int amount = kvp.Value;

                if (color == "Colorless") continue;

                switch (color)
                {
                    case "White": White -= amount; break;
                    case "Blue": Blue -= amount; break;
                    case "Black": Black -= amount; break;
                    case "Red": Red -= amount; break;
                    case "Green": Green -= amount; break;
                    case "Colorless": Colorless -= amount; break;
                }
            }

            // Spend generic cost from any available mana
            SpendGeneric(genericToSpend);
        }

        public void SpendGeneric(int amount)
        {
            int remaining = amount;

            int[] pools = { White, Blue, Black, Red, Green, Colorless };
            Action<int>[] setters =
            {
                    v => White = v,
                    v => Blue = v,
                    v => Black = v,
                    v => Red = v,
                    v => Green = v,
                    v => Colorless = v
                };

            for (int i = 0; i < pools.Length && remaining > 0; i++)
            {
                int used = Mathf.Min(pools[i], remaining);
                pools[i] -= used;
                remaining -= used;
            }

            for (int i = 0; i < pools.Length; i++)
                setters[i](pools[i]);

            if (remaining > 0)
            {
                Debug.LogWarning("Tried to spend more generic mana than available. This should not happen if CanPay() was used.");
            }
        }

        public int SpendAll()
        {
            int total = Total();
            SpendGeneric(total);
            return total;
        }

        public static int SpendFromPool(ref int pool, int needed)
        {
            int spent = Mathf.Min(pool, needed);
            pool -= spent;
            return spent;
        }
    }

    public void PlayCard(Card card)
    {
        if (!Hand.Contains(card)) return;

        Hand.Remove(card);

        if (card.entersTapped)
        {
            card.isTapped = true;
            Debug.Log($"{card.cardName} enters the battlefield tapped.");
        }
        Battlefield.Add(card);
        Debug.Log($"{card.cardName} is entering the battlefield.");
        card.OnEnterPlay(this);
        if (card is LandCard)
        {
            GameManager.Instance.NotifyLandEntered(card, this);
        }
        if ((card is ArtifactCard) || (card is CreatureCard cc && cc.color.Contains("Artifact")))
        {
            GameManager.Instance.NotifyArtifactEntered(card, this);
        }
        if (card is EnchantmentCard)
        {
            GameManager.Instance.NotifyEnchantmentEntered(card, this);
        }
    }

    public void SendToGraveyard(Card card)
    {
        Battlefield.Remove(card);
        Debug.Log($"{card.cardName} is being sent to the graveyard.");
        card.OnLeavePlay(this);
        if (card is LandCard)
            GameManager.Instance.NotifyLandLeft(card, this);
        Graveyard.Add(card);
    }

    public void DiscardRandomCard(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            if (Hand.Count == 0)
            {
                Debug.Log("Player has no cards left to discard.");
                return;
            }

            int index = UnityEngine.Random.Range(0, Hand.Count);
            Card discarded = Hand[index];

            Debug.Log($"{discarded.cardName} was randomly discarded.");
            GameManager.Instance.SendToGraveyard(discarded, this);
        }

        if (GameManager.Instance.enemyHandText != null && this == GameManager.Instance.aiPlayer)
            GameManager.Instance.enemyHandText.text = "Hand: " + Hand.Count;
    }

    public void ChangeLife(int change)
    {
        Life += change;
    }
}