using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Card
{
    public string cardName;
    public string rarity;
    public int manaCost;
    public bool isToken = false;
    public bool isTapped = false;
    public bool entersTapped = false;

    // If true, this card has an additional variable mana cost "X".
    public bool hasXCost = false;

    // Holds the value paid for X when the card was cast.
    public int xValue = 0;

    public List<string> color = new List<string>();
    public string PrimaryColor => color.Count > 0 ? color[0] : "None";

    // e.g. "Human", "Wizard"
    public List<string> subtypes = new List<string>();

    public string rulesText;
    public string flavorText;

    // Artist credited for the card's artwork
    public string artist;

    public int plagueAmount;
    public int manaToGain;
    public int lifeToGain;
    public int manaToPayToActivate;
    public int cardsToDraw;
    public int damageToCreature;
    public int buffPower;
    public int buffToughness;
    public KeywordAbility keywordBuff = KeywordAbility.None;

    public string tokenToCreate;

    public Sprite artwork;

    public Player owner;

    // Stores a card temporarily controlled by this card and its original owner.
    public Card gainedControlCard;
    public Player gainedControlCardOriginalOwner;

    public List<CardAbility> abilities = new List<CardAbility>();
    public List<ActivatedAbility> activatedAbilities = new List<ActivatedAbility>();
    public List<KeywordAbility> keywordAbilities = new List<KeywordAbility>();

    public virtual void Play(Player player)
    {
        if (entersTapped)
        {
            isTapped = true;
            Debug.Log($"{cardName} enters tapped.");
        }

        player.PlayCard(this);
    }

    public virtual void OnEnterPlay(Player owner)
    {
        foreach (var ability in abilities)
        {
            if (ability.timing != TriggerTiming.OnEnter)
                continue;

            if (ability.requiresTarget)
            {
                // Only initiate optional targeting for the human player's cards.
                // AI-controlled cards handle their ETB targeting automatically.
                if (owner == GameManager.Instance.humanPlayer)
                    GameManager.Instance.BeginOptionalTargetSelectionAfterEntry(this, owner, ability);
            }
            else
            {
                if (ability.effect != null)
                {
                    int oldLife = owner.Life;
                    ability.effect.Invoke(owner, this);
                    int gained = owner.Life - oldLife;

                    if (gained > 0)
                    {
                        VisualEffectManager.Instance.ShowFloatingHealForPlayer(gained,
                            owner == GameManager.Instance.humanPlayer);
                    }
                }
            }
        }
    }

    public virtual void OnLeavePlay(Player owner)
    {
        foreach (var ability in abilities)
        {
            if (ability.timing != TriggerTiming.OnDeath || ability.effect == null)
                continue;
            GameManager.Instance.QueueTriggeredAbility(ability, owner, this, this);
        }
    }

    public virtual string GetCardText()
    {
        List<string> lines = new List<string>();

        if (this is AuraCard aura)
        {
            string enchantText = aura.requiredTargetType == SorceryCard.TargetType.TappedCreature
                ? "Enchant tapped creature"
                : "Enchant creature";
            if (aura.targetMustBeControlledCreature)
                enchantText += " you control";
            lines.Add(enchantText);
            if (aura.gainControlOfCreature)
                lines.Add("You control enchanted creature.");
        }

        // Keyword abilities — only for creatures
        if (this is CreatureCard creature)
        {
            foreach (var keyword in creature.keywordAbilities)
            {
                if (keyword == KeywordAbility.CantBlock ||
                    keyword == KeywordAbility.CanOnlyBlockFlying ||
                    keyword == KeywordAbility.CantBlockWithoutForest ||
                    keyword == KeywordAbility.CantDealCombatDamage ||
                    keyword == KeywordAbility.BeastCreatureSpellsCostOneLess ||
                    keyword == KeywordAbility.PotionSpellsCostOneLess ||
                    keyword.ToString().StartsWith("ProtectionFrom"))
                    continue;

                lines.Add(keyword.ToString());
            }

            if (creature.keywordAbilities.Contains(KeywordAbility.CanOnlyBlockFlying))
                lines.Add("This creature can only block creatures with flying.");
            if (creature.keywordAbilities.Contains(KeywordAbility.CantBlock))
                lines.Add("This creature can't block.");
            if (creature.keywordAbilities.Contains(KeywordAbility.CantBlockWithoutForest))
                lines.Add("This creature can't block if you don't control a forest.");
            if (entersTapped)
                lines.Add("This creature enters the battlefield tapped.");
            if (creature.keywordAbilities.Contains(KeywordAbility.CantUntap))
                lines.Add("This creature doesn't untap during its controller's untap step.");
            if (creature.keywordAbilities.Contains(KeywordAbility.CantDealCombatDamage))
                lines.Add("This creature can't deal combat damage.");
            if (creature.keywordAbilities.Contains(KeywordAbility.ProtectionFromWhite))
                lines.Add("Protection from White");
            if (creature.keywordAbilities.Contains(KeywordAbility.ProtectionFromBlue))
                lines.Add("Protection from Blue");
            if (creature.keywordAbilities.Contains(KeywordAbility.ProtectionFromBlack))
                lines.Add("Protection from Black");
            if (creature.keywordAbilities.Contains(KeywordAbility.ProtectionFromRed))
                lines.Add("Protection from Red");
            if (creature.keywordAbilities.Contains(KeywordAbility.ProtectionFromGreen))
                lines.Add("Protection from Green");

            // Activated abilities
            if (creature.activatedAbilities != null)
            {
                Debug.Log($"{creature.cardName} has {creature.activatedAbilities.Count} activated abilities.");

                foreach (var activated in creature.activatedAbilities)
                {
                    Debug.Log($"Activated ability: {activated}");

                    switch (activated)
                    {
                        case ActivatedAbility.TapForMana:
                            lines.Add("Tap: Add 1 mana.");
                            break;
                        case ActivatedAbility.TapToLoseLife:
                            lines.Add($"Tap: Your opponent loses {creature.tapLifeLossAmount} life.");
                            break;
                        case ActivatedAbility.TapToCreateToken:
                            lines.Add($"{creature.manaToPayToActivate}TAP: Create a {tokenToCreate} token.");
                            break;
                        case ActivatedAbility.PayToGainAbility:
                            lines.Add($"{creature.manaToPayToActivate}: Gains {creature.abilityToGain} until end of turn.");
                            break;
                        case ActivatedAbility.PayToBuffSelf:
                            lines.Add($"{creature.manaToPayToActivate}: +1/+0 until end of turn.");
                            break;
                        case ActivatedAbility.ReturnSelfFromGraveyard:
                            lines.Add($"{creature.manaToPayToActivate}: Return this card from your graveyard to the battlefield.");
                            break;
                        case ActivatedAbility.ReturnSelfFromGraveyardToHand:
                            lines.Add($"{creature.manaToPayToActivate}: Return this card from your graveyard to your hand.");
                            break;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(rulesText))
        {
            lines.Add(rulesText);
        }

        if (this is ArtifactCard artifact)
        {
            if (entersTapped)
                lines.Add("This card enters the battlefield tapped.");

            if (activatedAbilities != null)
            {
                foreach (var activated in activatedAbilities)
                {
                    switch (activated)
                    {
                        case ActivatedAbility.TapForMana:
                            lines.Add("Tap: Add 1 mana.");
                            break;
                        case ActivatedAbility.TapToGainLife:
                            lines.Add("Tap: Gain 1 life.");
                            //lines.Add($"Tap: Gain {plagueAmount} life.");
                            break;
                        case ActivatedAbility.TapAndSacrificeForMana:
                            lines.Add("Tap, sacrifice: Add 1 mana.");
                            break;
                        case ActivatedAbility.TapToPlague:
                            lines.Add($"Tap: Each player loses {plagueAmount} life.");
                            break;
                        case ActivatedAbility.SacrificeForMana:
                            lines.Add($"{manaToPayToActivate}TAP, sacrifice: Add {manaToGain}.");
                            break;
                        case ActivatedAbility.SacrificeForLife:
                            lines.Add($"{manaToPayToActivate}TAP, sacrifice: Gain {lifeToGain} life.");
                            break;
                        case ActivatedAbility.SacrificeToDrawCards:
                            lines.Add($"{manaToPayToActivate}TAP, sacrifice: Draw {cardsToDraw} card(s).");
                            break;
                        case ActivatedAbility.DealDamageToCreature:
                            lines.Add($"{manaToPayToActivate}TAP, sacrifice: Deal {damageToCreature} damage to target creature.");
                            break;
                        case ActivatedAbility.BuffTargetCreature:
                            lines.Add($"{manaToPayToActivate}TAP, sacrifice: Target creature gets +{buffPower}/+{buffToughness} until end of turn.");
                            break;
                        case ActivatedAbility.TapToPlayRandomPotion:
                            lines.Add($"{manaToPayToActivate}TAP: Search your library for a random Potion and put it onto the battlefield, then shuffle.");
                            break;
                        case ActivatedAbility.Equip:
                            lines.Add($"Equip {manaToPayToActivate}");
                            break;
                    }
                }
            }
        }
        // Triggered abilities — shared across all cards
        foreach (var ability in abilities)
        {
            if (string.IsNullOrEmpty(ability.description))
                continue;

            if (ability.timing == TriggerTiming.OnEnter)
                lines.Add("When this creature enters, " + ability.description);
            else if (ability.timing == TriggerTiming.OnDeath)
                lines.Add("When this creature dies, " + ability.description);
            else if (ability.timing == TriggerTiming.OnUpkeep)
                lines.Add("At the beginning of your upkeep, " + ability.description);
            else if (ability.timing == TriggerTiming.OnArtifactEnter)
                lines.Add("Whenever an artifact enters the battlefield, " + ability.description);
            else if (ability.timing == TriggerTiming.OnEnchantmentEnter)
                lines.Add("Whenever an enchantment enters the battlefield, " + ability.description);
            else if (ability.timing == TriggerTiming.OnLandEnter)
                lines.Add("Whenever a land enters the battlefield, " + ability.description);
            else if (ability.timing == TriggerTiming.OnCreatureEnter)
                lines.Add("Whenever a creature enters the battlefield, " + ability.description);
            else if (ability.timing == TriggerTiming.OnLandLeave)
                lines.Add("Whenever a land leaves the battlefield, " + ability.description);
            else if (ability.timing == TriggerTiming.OnLifeGain)
                lines.Add("Whenever you gain life, " + ability.description);
            else if (ability.timing == TriggerTiming.OnCardDraw)
                lines.Add("Whenever you draw a card, " + ability.description);
            else if (ability.timing == TriggerTiming.OnOpponentDraw)
                lines.Add("Whenever an opponent draws a card, " + ability.description);
            else if (ability.timing == TriggerTiming.OnCreatureDies)
                lines.Add("Whenever a creature dies, " + ability.description);
            else if (ability.timing == TriggerTiming.OnCreatureDiesOrDiscarded)
                lines.Add("Whenever a creature dies or is discarded, " + ability.description);
            else if (ability.timing == TriggerTiming.OnPlayerDiscard)
                lines.Add("Whenever a player discards a card, " + ability.description);
            else if (ability.timing == TriggerTiming.OnBlock)
                lines.Add("When this creature blocks, " + ability.description);
            else if (ability.timing == TriggerTiming.OnCombatDamageToPlayer)
                lines.Add("Whenever a creature deals combat damage to a player, " + ability.description);
            else if (ability.timing == TriggerTiming.OnOpponentDiscard)
                lines.Add("Whenever an opponent discards a card, " + ability.description);
        }

        if (keywordAbilities != null)
        {
            if (keywordAbilities.Contains(KeywordAbility.AllPermanentsEnterTapped))
                lines.Add("All permanents enter the battlefield tapped.");
            if (keywordAbilities.Contains(KeywordAbility.NoLifeGain))
                lines.Add("Players can't gain life.");
            if (keywordAbilities.Contains(KeywordAbility.OnlyCastCreatureSpells))
                lines.Add("Players can only cast creature spells.");
            if (keywordAbilities.Contains(KeywordAbility.CreatureSpellsCostOneLess))
                lines.Add("Creature spells you cast cost 1 less.");
            if (keywordAbilities.Contains(KeywordAbility.BeastCreatureSpellsCostOneLess))
                lines.Add("Beast creature spells you cast cost 1 less.");
            if (keywordAbilities.Contains(KeywordAbility.PotionSpellsCostOneLess))
                lines.Add("Potion spells you cast cost 1 less to cast.");
            if (keywordAbilities.Contains(KeywordAbility.OpponentSpellsCostOneMore))
                lines.Add("Spells cast by your opponent cost 1 more.");
        }

        if (!string.IsNullOrEmpty(flavorText))
            lines.Add($"<i>{flavorText}</i>");

        return string.Join("\n", lines);
    }
}
