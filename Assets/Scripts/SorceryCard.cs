using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class SorceryCard : Card
{
    public bool requiresTarget = false;
    public int lifeToLoseForOpponent = 0;
    public int lifeLossForBothPlayers = 0;
    public int cardsToDiscardorDraw = 0;
    public bool drawIfOpponentCantDiscard = true;
    public int damageToEachCreatureAndPlayer = 0;
    public int manaToGainMin = 0;
    public int manaToGainMax = 0;
    public bool eachPlayerGainLifeEqualToLands = false;
    public bool exileAllCreaturesFromGraveyards = false;
    public bool swapGraveyardAndLibrary = false;
    public bool revealUntilCreature = false;
    public bool revealUntilLand = false;
    public bool returnRandomCreatureFromGraveyard = false;
    public bool returnRandomCheapCreatureToBattlefield = false;
    public int maxManaCostForReturn = 0;
    public int numberOfTokensMin = 0;
    public int numberOfTokensMax = 0;
    public int cardsToDrawMin = 0;
    public int cardsToDrawMax = 0;
    public Card chosenTarget = null;
    public int damageToTarget = 0;
    public int damageToTargetMin = 0;
    public int damageToTargetMax = 0;
    public bool destroyTargetIfTypeMatches = false;
    public bool destroyAllWithSameName = false;
    public KeywordAbility keywordToGrant = KeywordAbility.None;
    public string requiredTargetColor = null;
    public bool excludeArtifactCreatures = false;
    public Player chosenPlayerTarget = null;
    public bool addXPlusOneCounters = false;
    public bool addXMinusOneCounters = false;

    public TargetType requiredTargetType = TargetType.None;
    public PermanentTypeToDestroy typeOfPermanentToDestroyAll = PermanentTypeToDestroy.None;

    public enum TargetType
    {
        None,
        Creature,
        TappedCreature,
        Land,
        Artifact,
        Enchantment,
        Player,
        CreatureOrPlayer
    }

    public enum PermanentTypeToDestroy
    {
        None,
        Land,
        Creature,
        Artifact,
        Enchantment,
        // Add more as needed later (Artifacts, Enchantments, etc.)
    }

    public virtual void ResolveEffect(Player caster)
    {
        bool didSomething = false;

        if (revealUntilCreature)
        {
            GameManager.Instance.pendingStackEffects++;
            GameManager.Instance.StartCoroutine(GameManager.Instance.RevealUntilCreature(caster));
            didSomething = true;
        }

        if (revealUntilLand)
        {
            GameManager.Instance.pendingStackEffects++;
            GameManager.Instance.StartCoroutine(GameManager.Instance.RevealUntilLand(caster));
            didSomething = true;
        }

        if (!string.IsNullOrEmpty(tokenToCreate) && numberOfTokensMax > 0)
        {
            int amount = (numberOfTokensMin == numberOfTokensMax)
                ? numberOfTokensMin
                : Random.Range(numberOfTokensMin, numberOfTokensMax + 1);
            for (int i = 0; i < amount; i++)
            {
                Card token = CardFactory.Create(tokenToCreate);
                if (token != null)
                {
                    GameManager.Instance.SummonToken(token, caster);
                }
            }

            Debug.Log($"Spawned {amount} {tokenToCreate} tokens.");
        }

        if (returnRandomCreatureFromGraveyard)
        {
            GameManager.Instance.ReturnRandomCreatureFromGraveyard(caster);
            didSomething = true;
        }

        if (returnRandomCheapCreatureToBattlefield)
        {
            GameManager.Instance.ReturnRandomCreatureFromGraveyardToBattlefield(caster, maxManaCostForReturn);
            didSomething = true;
        }

        if (lifeToGain > 0)
        {
            GameManager.Instance.TryGainLife(caster, lifeToGain);
            Debug.Log($"{caster} gains {lifeToGain} life.");
            didSomething = true;
        }
        if (manaToGainMax > 0)
        {
            int amount = (manaToGainMin == manaToGainMax)
                ? manaToGainMin
                : Random.Range(manaToGainMin, manaToGainMax + 1);

            switch (PrimaryColor)
            {
                case "White": caster.ColoredMana.White += amount; break;
                case "Blue": caster.ColoredMana.Blue += amount; break;
                case "Black": caster.ColoredMana.Black += amount; break;
                case "Red": caster.ColoredMana.Red += amount; break;
                case "Green": caster.ColoredMana.Green += amount; break;
                default: caster.ColoredMana.Colorless += amount; break;
            }

            Debug.Log($"{caster} gains {amount} {PrimaryColor} mana.");
            GameManager.Instance.UpdateUI();
            didSomething = true;
        }
        if (lifeToLoseForOpponent > 0)
        {
            Player opponent = GameManager.Instance.GetOpponentOf(caster);
            opponent.Life -= lifeToLoseForOpponent;
            Debug.Log($"{opponent} loses {lifeToLoseForOpponent} life.");

            VisualEffectManager.Instance.ShowFloatingDamageForPlayer(lifeToLoseForOpponent, opponent == GameManager.Instance.humanPlayer);
            GameManager.Instance.CheckForGameEnd();
            didSomething = true;
        }
        if (lifeLossForBothPlayers > 0)
        {
            GameManager.Instance.humanPlayer.Life -= lifeLossForBothPlayers;
            GameManager.Instance.aiPlayer.Life -= lifeLossForBothPlayers;
            Debug.Log($"Each player loses {lifeLossForBothPlayers} life.");

            VisualEffectManager.Instance.ShowFloatingDamageForPlayer(lifeLossForBothPlayers, true);
            VisualEffectManager.Instance.ShowFloatingDamageForPlayer(lifeLossForBothPlayers, false);
            GameManager.Instance.CheckForGameEnd();
            didSomething = true;
        }
        if (cardsToDrawMax > 0)
        {
            int amount = (cardsToDrawMin == cardsToDrawMax)
                ? cardsToDrawMin
                : Random.Range(cardsToDrawMin, cardsToDrawMax + 1);
            GameManager.Instance.DrawCards(caster, amount);
            Debug.Log($"{caster} draws {amount} card(s).");
            didSomething = true;
        }
        else if (cardsToDraw > 0)
        {
            GameManager.Instance.DrawCards(caster, cardsToDraw);
            Debug.Log($"{caster} draws {cardsToDraw} card(s).");
            didSomething = true;
        }
        if (cardsToDiscardorDraw > 0)
        {
            Player opponent = GameManager.Instance.GetOpponentOf(caster);
            bool opponentDiscarded = false;

            for (int i = 0; i < cardsToDiscardorDraw; i++)
            {
                if (opponent.Hand.Count > 0)
                {
                    Card toDiscard = opponent.Hand[Random.Range(0, opponent.Hand.Count)];
                    GameManager.Instance.SendToGraveyard(toDiscard, opponent);
                    Debug.Log($"{opponent} discarded {toDiscard.cardName}");
                    opponentDiscarded = true;
                }
                else
                {
                    Debug.Log($"{opponent} has no cards to discard.");
                }
            }

            if (!opponentDiscarded && drawIfOpponentCantDiscard)
            {
                GameManager.Instance.DrawCard(caster);
                Debug.Log($"{caster} draws a card because opponent had nothing to discard.");
            }

            didSomething = true;
        }
        if (eachPlayerGainLifeEqualToLands)
        {
            Player human = GameManager.Instance.humanPlayer;
            Player ai = GameManager.Instance.aiPlayer;

            int humanLands = human.Battlefield.Count(card => card is LandCard);
            int aiLands = ai.Battlefield.Count(card => card is LandCard);

            GameManager.Instance.TryGainLife(human, humanLands);
            GameManager.Instance.TryGainLife(ai, aiLands);

            Debug.Log($"Each player gains life equal to their own lands. Human: +{humanLands}, AI: +{aiLands}");
            didSomething = true;
        }
        if (typeOfPermanentToDestroyAll != PermanentTypeToDestroy.None)
        {
            List<(Card card, Player owner)> destroyedCards = new List<(Card, Player)>();

            foreach (var player in new[] { GameManager.Instance.humanPlayer, GameManager.Instance.aiPlayer })
            {
                var targets = player.Battlefield
                    .Where(card =>
                    {
                        if (card.keywordAbilities.Contains(KeywordAbility.Indestructible))
                            return false;

                        if (typeOfPermanentToDestroyAll == PermanentTypeToDestroy.Land && card is LandCard)
                            return true;

                        if (typeOfPermanentToDestroyAll == PermanentTypeToDestroy.Creature && card is CreatureCard)
                            return true;

                        if (typeOfPermanentToDestroyAll == PermanentTypeToDestroy.Artifact)
                        {
                            if (card is ArtifactCard)
                                return true;

                            if (card is CreatureCard)
                            {
                                var data = CardDatabase.GetCardData(card.cardName);
                                if (data != null && data.color.Contains("Artifact"))
                                    return true;
                            }
                        }

                        if (typeOfPermanentToDestroyAll == PermanentTypeToDestroy.Enchantment && card is EnchantmentCard)
                            return true;

                        return false;
                    })
                    .ToList();

                foreach (var card in targets)
                {
                    destroyedCards.Add((card, player));
                }
            }

            foreach (var (card, owner) in destroyedCards)
            {
                GameManager.Instance.SendToGraveyard(card, owner);
            }

            Debug.Log($"Destroyed all {typeOfPermanentToDestroyAll}s: {string.Join(", ", destroyedCards.Select(c => c.card.cardName))}");
            didSomething = true;
        }
        if (exileAllCreaturesFromGraveyards)
        {
            List<Card> exiledCards = new List<Card>();

            foreach (var player in new[] { GameManager.Instance.humanPlayer, GameManager.Instance.aiPlayer })
            {
                var toRemove = player.Graveyard
                    .Where(c => c is CreatureCard)
                    .ToList();

                foreach (var card in toRemove)
                {
                    player.Graveyard.Remove(card);

                    CardVisual visual = GameManager.Instance.FindCardVisual(card);
                    if (visual != null)
                    {
                        GameManager.Instance.activeCardVisuals.Remove(visual);
                        GameObject.Destroy(visual.gameObject);
                    }

                    exiledCards.Add(card);
                }
            }

            Debug.Log($"Exiled creatures from graveyards: {string.Join(", ", exiledCards.Select(c => c.cardName))}");
            didSomething = true;
        }
        if (damageToEachCreatureAndPlayer > 0)
        {
            foreach (var player in new[] { GameManager.Instance.humanPlayer, GameManager.Instance.aiPlayer })
            {
                // Damage to player
                player.Life -= damageToEachCreatureAndPlayer;
                VisualEffectManager.Instance.ShowFloatingDamageForPlayer(damageToEachCreatureAndPlayer, player == GameManager.Instance.humanPlayer);

                // Damage to each creature
                foreach (var creature in player.Battlefield.OfType<CreatureCard>())
                {
                    KeywordAbility protection = ProtectionUtils.GetProtectionKeyword(this.PrimaryColor);

                    if (creature.keywordAbilities.Contains(protection))
                    {
                        continue;
                    }

                    creature.TakeDamage(damageToEachCreatureAndPlayer);
                }
            }

            GameManager.Instance.CheckDeaths(GameManager.Instance.humanPlayer);
            GameManager.Instance.CheckDeaths(GameManager.Instance.aiPlayer);
            GameManager.Instance.CheckForGameEnd();
            didSomething = true;
        }
        if (swapGraveyardAndLibrary)
        {
            foreach (var player in new[] { GameManager.Instance.humanPlayer, GameManager.Instance.aiPlayer })
            {
                List<Card> oldDeck = new List<Card>(player.Deck);
                player.Deck = new List<Card>(player.Graveyard);
                player.Graveyard = oldDeck;

                for (int i = 0; i < player.Deck.Count; i++)
                {
                    Card temp = player.Deck[i];
                    int rand = Random.Range(i, player.Deck.Count);
                    player.Deck[i] = player.Deck[rand];
                    player.Deck[rand] = temp;
                }

                GameManager.Instance.RefreshGraveyardVisuals(player);
            }

            Debug.Log("Graveyards and libraries swapped and shuffled.");
            didSomething = true;
        }
        GameManager.Instance.UpdateUI();
    }

    public virtual void ResolveEffect(Player caster, Card target)
    {
        int dmg = 0; // Declare outside so it's visible throughout the method

        if (target != null)
        {
            dmg = damageToTargetMax > 0
                ? (damageToTargetMin == damageToTargetMax
                    ? damageToTargetMin
                    : Random.Range(damageToTargetMin, damageToTargetMax + 1))
                : damageToTarget;

            if (dmg > 0 && target is CreatureCard creature)
            {
                KeywordAbility protection = ProtectionUtils.GetProtectionKeyword(PrimaryColor);
                if (creature.keywordAbilities.Contains(protection))
                {
                    Debug.Log($"{creature.cardName} is protected from {color}, takes no damage.");
                }
                else
                {
                    creature.TakeDamage(dmg);
                    GameManager.Instance.CheckDeaths(GameManager.Instance.humanPlayer);
                    GameManager.Instance.CheckDeaths(GameManager.Instance.aiPlayer);
                }

                GameManager.Instance.UpdateUI();
                ResolveEffect(caster);
                return;
            }

            if (destroyAllWithSameName && target is CreatureCard)
            {
                string name = target.cardName;
                List<(Card card, Player owner)> toDestroy = new List<(Card, Player)>();
                foreach (var player in new[] { GameManager.Instance.humanPlayer, GameManager.Instance.aiPlayer })
                {
                    foreach (var card in player.Battlefield.OfType<CreatureCard>()
                        .Where(c => c.cardName == name && !c.keywordAbilities.Contains(KeywordAbility.Indestructible))
                        .ToList())
                    {
                        toDestroy.Add((card, player));
                    }
                }
                foreach (var (card, owner) in toDestroy)
                {
                    GameManager.Instance.SendToGraveyard(card, owner);
                }

                Debug.Log($"{cardName} destroyed {toDestroy.Count} copies of {name}.");

                ResolveEffect(caster);
                return;
            }

            if (destroyTargetIfTypeMatches)
            {
                bool typeMatches =
                    (requiredTargetType == TargetType.Creature && target is CreatureCard targetCreature &&
                        !(excludeArtifactCreatures && targetCreature.color.Contains("Artifact"))) ||
                    (requiredTargetType == TargetType.Land && target is LandCard) ||
                    (requiredTargetType == TargetType.Artifact && target is ArtifactCard) ||
                    (requiredTargetType == TargetType.Enchantment && target is EnchantmentCard);

                bool colorMatches = true;

                if (!string.IsNullOrEmpty(requiredTargetColor))
                {
                    CardData data = CardDatabase.GetCardData(target.cardName);
                    colorMatches = data != null && data.color.Contains(requiredTargetColor);
                }

                if (typeMatches && colorMatches)
                {
                    if (target.keywordAbilities.Contains(KeywordAbility.Indestructible))
                    {
                        Debug.Log($"{cardName} failed to destroy {target.cardName}: indestructible.");
                    }
                    else
                    {
                        GameManager.Instance.SendToGraveyard(target, GameManager.Instance.GetOwnerOfCard(target));
                        Debug.Log($"{cardName} destroyed {target.cardName}.");
                    }

                    ResolveEffect(caster);
                    return;
                }
                else
                {
                    Debug.LogWarning($"{cardName} failed to destroy {target.cardName}: type match = {typeMatches}, color match = {colorMatches}");
                }
            }
        }

        if (keywordToGrant != KeywordAbility.None && target is CreatureCard keywordCreature)
        {
            if (!keywordCreature.keywordAbilities.Contains(keywordToGrant))
                keywordCreature.keywordAbilities.Add(keywordToGrant);

            if (!keywordCreature.temporaryKeywordAbilities.Contains(keywordToGrant))
                keywordCreature.temporaryKeywordAbilities.Add(keywordToGrant);

            if (keywordToGrant == KeywordAbility.Haste)
                keywordCreature.hasSummoningSickness = false;

            var visual = GameManager.Instance.FindCardVisual(keywordCreature);
            if (visual != null)
                visual.UpdateVisual();

            Debug.Log($"{keywordCreature.cardName} gains {keywordToGrant} until end of turn.");
        }

        if ((buffPower != 0 || buffToughness != 0) && target is CreatureCard buffCreature)
        {
            buffCreature.AddTemporaryBuff(buffPower, buffToughness);
            var visual = GameManager.Instance.FindCardVisual(buffCreature);
            if (visual != null)
                visual.UpdateVisual();

            Debug.Log($"{buffCreature.cardName} gets +{buffPower}/+{buffToughness} until end of turn.");
        }

        if (addXPlusOneCounters && target is CreatureCard plusTarget && xValue > 0)
        {
            for (int i = 0; i < xValue; i++)
                plusTarget.AddPlusOneCounter();

            var visual = GameManager.Instance.FindCardVisual(plusTarget);
            if (visual != null)
                visual.UpdateVisual();

            Debug.Log($"{plusTarget.cardName} receives {xValue} +1/+1 counters.");
        }

        if (addXMinusOneCounters && target is CreatureCard minusTarget && xValue > 0)
        {
            for (int i = 0; i < xValue; i++)
                minusTarget.AddMinusOneCounter();

            var visual = GameManager.Instance.FindCardVisual(minusTarget);
            if (visual != null)
                visual.UpdateVisual();

            GameManager.Instance.CheckDeaths(GameManager.Instance.humanPlayer);
            GameManager.Instance.CheckDeaths(GameManager.Instance.aiPlayer);

            Debug.Log($"{minusTarget.cardName} receives {xValue} -1/-1 counters.");
        }
        else if (!destroyTargetIfTypeMatches && dmg <= 0 && keywordToGrant == KeywordAbility.None)
        {
            Debug.LogWarning($"{cardName} resolved on {target?.cardName ?? "null"}, but did nothing.");
        }

        GameManager.Instance.UpdateUI();
        ResolveEffect(caster);
    }



    public virtual void ResolveEffectOnPlayer(Player caster, Player targetPlayer)
    {
        if (requiredTargetType == TargetType.Player || requiredTargetType == TargetType.CreatureOrPlayer)
        {
            int dmg = damageToTargetMax > 0
                ? (damageToTargetMin == damageToTargetMax
                    ? damageToTargetMin
                    : Random.Range(damageToTargetMin, damageToTargetMax + 1))
                : damageToTarget;

            if (dmg > 0)
            {
                targetPlayer.Life -= dmg;
                Debug.Log($"{cardName} deals {dmg} damage to {targetPlayer}.");

                GameManager.Instance.CheckForGameEnd();
                VisualEffectManager.Instance.ShowFloatingDamageForPlayer(dmg, (targetPlayer == GameManager.Instance.humanPlayer));
            }
        }

        GameManager.Instance.UpdateUI();
        ResolveEffect(caster);
    }
}