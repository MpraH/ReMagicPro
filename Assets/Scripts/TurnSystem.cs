using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class TurnSystem : MonoBehaviour
{
    public static TurnSystem Instance { get; private set; }

    public bool autoStart = false;

    public enum TurnPhase
    {
        StartTurn,
        Untap,
        Upkeep,
        Draw,
        Main1,
        EnterCombat,
        ChooseAttackers,
        ConfirmAttackers,
        ChooseBlockers,
        ConfirmBlockers,
        Damage,
        Main2,
        EndTurn
    }

    public enum PlayerType
    {
        Human,
        AI
    }

    public PlayerType currentPlayer = PlayerType.Human;
    public TurnPhase currentPhase = TurnPhase.StartTurn;

    public bool waitingForPlayerInput = false;
    private bool waitingForAIAction = false;
    public TMP_Text phaseText;
    public GameObject turnBanner;
    private bool firstTurn = true;
    private bool skipDrawThisTurn = false;

    [Header("Buttons")]
    public Button nextPhaseButton;
    public Button confirmAttackersButton;
    public Button confirmBlockersButton;
    public Button attackAllButton;
    public Button clearAttackersButton;

    public TurnPhase lastPhaseBeforeStack;
    public bool waitingToResumeAI = false;

    private Coroutine damageCoroutine;

    void Start()
    {
        Instance = this;

        nextPhaseButton.onClick.AddListener(NextPhaseButton);
        confirmAttackersButton.onClick.AddListener(ConfirmAttackers);
        confirmBlockersButton.onClick.AddListener(ConfirmBlockers);
        attackAllButton.onClick.AddListener(SelectAllEligibleAttackers);
        clearAttackersButton.onClick.AddListener(ClearAllSelectedAttackers);

        confirmAttackersButton.gameObject.SetActive(false);
        confirmBlockersButton.gameObject.SetActive(false);
        attackAllButton.gameObject.SetActive(false);
        clearAttackersButton.gameObject.SetActive(false);

        if (turnBanner != null)
            turnBanner.SetActive(false);

        if (autoStart)
            StartGame();
    }

    public void StartGame()
    {
        PlayerType startingPlayer = Random.value < 0.5f ? PlayerType.Human : PlayerType.AI;
        skipDrawThisTurn = true;
        BeginTurn(startingPlayer);
    }

    void Update()
    {
        if (GameManager.Instance.gameOver)
            return;

        if (currentPlayer == PlayerType.AI && !waitingForPlayerInput && !waitingForAIAction && !GameManager.Instance.IsStackActive())
        {
            RunCurrentPhase();
        }

        if (nextPhaseButton != null)
        {
            bool allowNext = currentPlayer == PlayerType.Human &&
                            waitingForPlayerInput &&
                            (!GameManager.Instance.IsStackActive() || GameManager.Instance.isTargetingMode) &&
                            !GameManager.Instance.graveyardViewActive &&
                            currentPhase != TurnPhase.ConfirmAttackers &&
                            currentPhase != TurnPhase.ChooseAttackers;

            nextPhaseButton.interactable = allowNext;

            TMP_Text label = nextPhaseButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = (currentPhase == TurnPhase.Main2) ? "END TURN" : "NEXT PHASE";
            }
        }

        // Handle spacebar shortcut
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (currentPlayer == PlayerType.Human &&
                waitingForPlayerInput &&
                (!GameManager.Instance.IsStackActive() || GameManager.Instance.isTargetingMode) &&
                !GameManager.Instance.graveyardViewActive &&
                currentPhase != TurnPhase.ConfirmAttackers &&
                currentPhase != TurnPhase.ChooseAttackers)
            {
                NextPhaseButton();
            }
        }
    }

    public void NextPhaseButton()
    {
        if (GameManager.Instance.gameOver)
            return;

        if (GameManager.Instance.graveyardViewActive)
            return;

        if (currentPlayer == PlayerType.Human && waitingForPlayerInput)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.buttonClick);

            if (GameManager.Instance.isTargetingMode)
            {
                Debug.Log("Canceled targeting because player pressed Next Phase.");
                GameManager.Instance.CancelTargeting();
            }
            if (GameManager.Instance.targetingCreatureOptional != null)
            {
                Debug.Log("Canceled optional ETB targeting because player pressed Next Phase.");
                GameManager.Instance.CancelOptionalTargeting();
            }

            waitingForPlayerInput = false;
            HideAllConfirmButtons();
            AdvancePhase();
        }
    }

    public void ConfirmAttackers()
    {
        if (GameManager.Instance.graveyardViewActive)
            return;

        if (waitingForPlayerInput)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.buttonClick);
            waitingForPlayerInput = false;
            confirmAttackersButton.gameObject.SetActive(false);
            attackAllButton.gameObject.SetActive(false);
            clearAttackersButton.gameObject.SetActive(false);

            // Use only manually selected attackers
            GameManager.Instance.currentAttackers.Clear();
            GameManager.Instance.currentAttackers.AddRange(GameManager.Instance.selectedAttackers);
            GameManager.Instance.selectedAttackers.Clear();

            foreach (var creature in GameManager.Instance.currentAttackers)
            {
                Debug.Log("Attacker declared: " + creature.cardName);
            }

            AdvancePhase();
        }
    }

    public void ConfirmBlockers()
    {
        if (GameManager.Instance.graveyardViewActive)
            return;

        if (waitingForPlayerInput)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.buttonClick);
            waitingForPlayerInput = false;
            confirmBlockersButton.gameObject.SetActive(false);
            AdvancePhase();
            GameManager.Instance.UpdateUI();
        }
    }

    void HideAllConfirmButtons()
    {
        confirmAttackersButton.gameObject.SetActive(false);
        confirmBlockersButton.gameObject.SetActive(false);
        attackAllButton.gameObject.SetActive(false);
        clearAttackersButton.gameObject.SetActive(false);
    }

    public void BeginTurn(PlayerType player)
    {
        if (GameManager.Instance.gameOver)
            return;

        currentPlayer = player;
        currentPhase = TurnPhase.StartTurn;
        Debug.Log($"\n=== {player} TURN START ===");

        GameManager.Instance.ResetDeathTracking();

        if (!firstTurn && turnBanner != null)
        {
            if (turnBanner.activeSelf)
                turnBanner.SetActive(false);

            turnBanner.SetActive(true);
            SoundManager.Instance.PlaySound(SoundManager.Instance.turnChange);
            StartCoroutine(WaitForBannerAndStart());
        }
        else
        {
            firstTurn = false;
            AdvancePhase();
        }
    }

    void AdvancePhase()
    {
        if (GameManager.Instance.gameOver)
            return;

        GameManager.Instance.UpdateUI();

        // Empty mana at the end of each phase
        var player = currentPlayer == PlayerType.Human
            ? GameManager.Instance.humanPlayer
            : GameManager.Instance.aiPlayer;

        player.ColoredMana.Clear();

        if (currentPlayer == PlayerType.Human)
            GameManager.Instance.UpdateUI();

        currentPhase++;
        RunCurrentPhase();
    }

    void RunCurrentPhase()
    {
        if (GameManager.Instance.gameOver)
            return;

        Debug.Log($"[Phase] {currentPlayer} - {currentPhase}");

        if (GameManager.Instance.IsStackActive())
        {
            Debug.Log("AI cast a sorcery — stack is busy. Will resume after.");
            lastPhaseBeforeStack = currentPhase;
            waitingToResumeAI = true;
            return;
        }

        string label = $"{currentPlayer} - {currentPhase}";
        if (phaseText != null)
            phaseText.text = label;

        switch (currentPhase)
        {
            case TurnPhase.Untap:
                Debug.Log("→ Untapping all permanents.");
                var p = (currentPlayer == PlayerType.Human) ? GameManager.Instance.humanPlayer : GameManager.Instance.aiPlayer;
                p.hasPlayedLandThisTurn = false;
                GameManager.Instance.ResetPermanents(currentPlayer == PlayerType.Human ? GameManager.Instance.humanPlayer : GameManager.Instance.aiPlayer);
                AdvancePhase();
                break;

            case TurnPhase.Upkeep:
                Debug.Log("→ Upkeep phase.");
                var player = currentPlayer == PlayerType.Human
                    ? GameManager.Instance.humanPlayer
                    : GameManager.Instance.aiPlayer;

                StartCoroutine(HandleUpkeepTriggers(player));
                break;

            case TurnPhase.Draw:
                if (skipDrawThisTurn)
                {
                    Debug.Log("→ Skipping draw this turn.");
                    skipDrawThisTurn = false;
                }
                else
                {
                    Debug.Log("→ Drawing a card.");

                    var drawPlayer = currentPlayer == PlayerType.Human
                        ? GameManager.Instance.humanPlayer
                        : GameManager.Instance.aiPlayer;

                    GameManager.Instance.DrawCard(drawPlayer);
                    GameManager.Instance.UpdateUI();
                }

                AdvancePhase();
                break;

            case TurnPhase.Main1:
            case TurnPhase.Main2:
                if (currentPlayer == PlayerType.Human)
                {
                    Debug.Log("→ Main Phase: Play land or cast spells.");
                    waitingForPlayerInput = true;
                }
                else
                {
                    Debug.Log("→ AI Main Phase: Playing cards.");

                    Player ai = GameManager.Instance.aiPlayer;

                    // Play 1 land if possible
                    if (!ai.hasPlayedLandThisTurn)
                    {
                        for (int i = 0; i < ai.Hand.Count; i++)
                        {
                            if (ai.Hand[i] is LandCard)
                            {
                                Card land = ai.Hand[i];

                                land.Play(ai);
                                ai.Hand.Remove(land);

                                if (land.entersTapped || GameManager.Instance.IsAllPermanentsEnterTappedActive())
                                {
                                    land.isTapped = true;
                                    Debug.Log($"{land.cardName} (AI) enters tapped (static effect or base).");
                                }

                                GameObject obj = GameObject.Instantiate(GameManager.Instance.cardPrefab, GameManager.Instance.aiLandArea);
                                CardVisual visual = obj.GetComponent<CardVisual>();
                                visual.Setup(land, GameManager.Instance);
                                visual.isInBattlefield = true;
                                GameManager.Instance.activeCardVisuals.Add(visual);

                                Debug.Log("AI played land: " + land.cardName);
                                ai.hasPlayedLandThisTurn = true;

                                waitingForAIAction = true;
                                StartCoroutine(WaitForAIAction(1f));
                                return;
                            }
                        }
                    }



                    // Play as many cards as AI can afford
                    bool playedCard = true;

                    while (playedCard && !GameManager.Instance.IsStackActive())
                    {
                        playedCard = false;

                        ai.Hand.Sort((a, b) =>
                        {
                            int costA = CardDatabase.GetCardData(a.cardName)?.manaCost ?? 0;
                            int costB = CardDatabase.GetCardData(b.cardName)?.manaCost ?? 0;
                            return costB.CompareTo(costA);
                        });

                        for (int i = 0; i < ai.Hand.Count; i++)
                        {
                            Card card = ai.Hand[i];

                            if (GameManager.Instance.IsOnlyCastCreatureSpellsActive() && !(card is CreatureCard))
                                continue;

                            if (card is CreatureCard creature)
                            {
                                var cost = GameManager.Instance.GetManaCostBreakdown(creature.manaCost, creature.color);
                                int tax = GameManager.Instance.GetOpponentSpellTax(ai);
                                if (tax > 0)
                                {
                                    if (!cost.ContainsKey("Colorless"))
                                        cost["Colorless"] = 0;
                                    cost["Colorless"] += tax;
                                }
                                int reduction = GameManager.Instance.GetCreatureCostReduction(ai);
                                CardData data = CardDatabase.GetCardData(creature.cardName);
                                if (data != null && data.subtypes.Contains("Beast"))
                                    reduction += GameManager.Instance.GetBeastCreatureCostReduction(ai);
                                if (reduction > 0 && cost.ContainsKey("Colorless"))
                                    cost["Colorless"] = Mathf.Max(0, cost["Colorless"] - reduction);
                                if (EnsureManaForCost(ai, cost))
                                {
                                    ai.ColoredMana.Pay(cost);
                                    if (card.hasXCost)
                                    {
                                        card.xValue = ai.ColoredMana.Total();
                                        if (card.xValue > 0)
                                            ai.ColoredMana.SpendGeneric(card.xValue);
                                    }
                                    ai.Hand.Remove(card);

                                    GameObject obj = GameObject.Instantiate(GameManager.Instance.cardPrefab, GameManager.Instance.stackZone);
                                    CardVisual visual = obj.GetComponent<CardVisual>();
                                    CardData cData = CardDatabase.GetCardData(card.cardName);
                                    visual.Setup(card, GameManager.Instance, cData);
                                    GameManager.Instance.activeCardVisuals.Add(visual);
                                    creature.owner = ai;

                                    visual.transform.localPosition = Vector3.zero;
                                    visual.transform.SetParent(GameManager.Instance.stackZone, false);
                                    visual.isInStack = true;

                                    GameManager.Instance.UpdateUI();
                                    SoundManager.Instance.PlaySound(SoundManager.Instance.cardPlay);

                                    GameManager.Instance.isStackBusy = true;
                                    TurnSystem.Instance.waitingToResumeAI = true;
                                    TurnSystem.Instance.lastPhaseBeforeStack = currentPhase;

                                    GameManager.Instance.StartCoroutine(GameManager.Instance.ResolveCreatureAfterDelay(creature, visual, ai));

                                    playedCard = true;
                                    break;
                                }
                            }
                            else if (card is SorceryCard sorcery)
                            {
                                var cost = GameManager.Instance.GetManaCostBreakdown(sorcery.manaCost, sorcery.color);
                                int tax = GameManager.Instance.GetOpponentSpellTax(ai);
                                if (tax > 0)
                                {
                                    if (!cost.ContainsKey("Colorless"))
                                        cost["Colorless"] = 0;
                                    cost["Colorless"] += tax;
                                }

                                var potential = GetPotentialManaPool(ai);
                                if (!potential.CanPay(cost))
                                    continue;

                                if (sorcery.requiredTargetType == SorceryCard.TargetType.Creature &&
                                    sorcery.destroyTargetIfTypeMatches)
                                {
                                    Player opponent = GameManager.Instance.GetOpponentOf(ai);

                                    // Pick enemy creature with the highest mana cost
                                    var target = opponent.Battlefield
                                        .OfType<CreatureCard>()
                                        .Where(c => !(sorcery.excludeArtifactCreatures && c.color.Contains("Artifact")))
                                        .OrderByDescending(c =>
                                        {
                                            var data = CardDatabase.GetCardData(c.cardName);
                                            return data != null ? data.manaCost : 0;
                                        })
                                        .FirstOrDefault();

                                    if (target != null)
                                    {
                                        sorcery.chosenTarget = target;
                                        sorcery.chosenPlayerTarget = null;

                                        Debug.Log($"AI targets {target.cardName} with {sorcery.cardName} (highest cost creature).");
                                    }
                                }
                                else if (sorcery.requiredTargetType == SorceryCard.TargetType.Artifact &&
                                        sorcery.destroyTargetIfTypeMatches)
                                {
                                    Player opponent = GameManager.Instance.GetOpponentOf(ai);

                                    var target = opponent.Battlefield
                                        .OfType<ArtifactCard>()
                                        .OrderByDescending(c =>
                                        {
                                            var data = CardDatabase.GetCardData(c.cardName);
                                            return data != null ? data.manaCost : 0;
                                        })
                                        .FirstOrDefault();

                                    if (target != null)
                                    {
                                        sorcery.chosenTarget = target;
                                        sorcery.chosenPlayerTarget = null;
                                        Debug.Log($"AI targets {target.cardName} with {sorcery.cardName} (highest cost artifact).");
                                    }
                                }
                                else if (sorcery.requiredTargetType == SorceryCard.TargetType.Enchantment &&
                                        sorcery.destroyTargetIfTypeMatches)
                                {
                                    Player opponent = GameManager.Instance.GetOpponentOf(ai);

                                    var target = opponent.Battlefield
                                        .OfType<EnchantmentCard>()
                                        .OrderByDescending(c =>
                                        {
                                            var data = CardDatabase.GetCardData(c.cardName);
                                            return data != null ? data.manaCost : 0;
                                        })
                                        .FirstOrDefault();

                                    if (target != null)
                                    {
                                        sorcery.chosenTarget = target;
                                        sorcery.chosenPlayerTarget = null;
                                        Debug.Log($"AI targets {target.cardName} with {sorcery.cardName} (highest cost enchantment).");
                                    }
                                }
                                else if (sorcery.requiredTargetType == SorceryCard.TargetType.Land &&
                                        sorcery.destroyTargetIfTypeMatches)
                                {
                                    Player opponent = GameManager.Instance.GetOpponentOf(ai);

                                    var target = opponent.Battlefield
                                        .OfType<LandCard>()
                                        .OrderByDescending(c =>
                                        {
                                            var data = CardDatabase.GetCardData(c.cardName);
                                            return data != null ? data.manaCost : 0;
                                        })
                                        .FirstOrDefault();

                                    if (target != null)
                                    {
                                        sorcery.chosenTarget = target;
                                        sorcery.chosenPlayerTarget = null;
                                        Debug.Log($"AI targets {target.cardName} with {sorcery.cardName} (highest cost land).");
                                    }
                                }

                                bool canTarget = sorcery.requiredTargetType == SorceryCard.TargetType.CreatureOrPlayer && (sorcery.damageToTarget > 0 || sorcery.damageToTargetMax > 0);

                                if (canTarget)
                                {
                                    int damage = sorcery.damageToTargetMax > 0 ? sorcery.damageToTargetMax : sorcery.damageToTarget;
                                    Player opponent = GameManager.Instance.humanPlayer;

                                    // Get enemy creatures
                                    List<CreatureCard> enemyCreatures = opponent.Battlefield
                                        .OfType<CreatureCard>()
                                        .Where(c => c.toughness > 0)
                                        .ToList();

                                    // 1. Kill opponent
                                    if (opponent.Life <= damage)
                                    {
                                        sorcery.chosenTarget = null;
                                        sorcery.chosenPlayerTarget = opponent;
                                    }
                                    // 2. Killable creature
                                    else
                                    {
                                        var killable = enemyCreatures.FirstOrDefault(c => c.toughness <= damage);
                                        if (killable != null)
                                        {
                                            sorcery.chosenTarget = killable;
                                        }
                                        else
                                        {
                                            // 3. Fallback: damage opponent
                                            sorcery.chosenTarget = null;
                                            sorcery.chosenPlayerTarget = opponent;

                                        }
                                    }
                                }

                                if (sorcery.requiresTarget && sorcery.chosenTarget == null && sorcery.chosenPlayerTarget == null)
                                {
                                    Debug.Log($"[AI] Skipping {sorcery.cardName} — no valid target.");
                                    continue; // Go to next card
                                }

                                if (!EnsureManaForCost(ai, cost))
                                    continue;

                                ai.ColoredMana.Pay(cost);
                                if (sorcery.hasXCost)
                                {
                                    sorcery.xValue = ai.ColoredMana.Total();
                                    if (sorcery.xValue > 0)
                                        ai.ColoredMana.SpendGeneric(sorcery.xValue);
                                }
                                ai.Hand.Remove(sorcery);
                                sorcery.owner = ai;

                                GameObject obj = GameObject.Instantiate(GameManager.Instance.cardPrefab, GameManager.Instance.stackZone);
                                CardVisual visual = obj.GetComponent<CardVisual>();
                                CardData data = CardDatabase.GetCardData(sorcery.cardName);
                                visual.Setup(sorcery, GameManager.Instance, data);

                                visual.transform.localPosition = Vector3.zero;
                                visual.transform.SetParent(GameManager.Instance.stackZone, false);
                                visual.isInStack = true;

                                GameManager.Instance.UpdateUI();
                                SoundManager.Instance.PlaySound(SoundManager.Instance.cardPlay);

                                GameManager.Instance.isStackBusy = true;
                                TurnSystem.Instance.waitingToResumeAI = true;
                                TurnSystem.Instance.lastPhaseBeforeStack = currentPhase;

                                GameManager.Instance.StartCoroutine(GameManager.Instance.ResolveSorceryAfterDelay(sorcery, visual, ai));

                                playedCard = true;
                                break;
                            }
                            else if (card is ArtifactCard artifact)
                            {
                                var cost = GameManager.Instance.GetManaCostBreakdown(artifact.manaCost, artifact.color);
                                int tax = GameManager.Instance.GetOpponentSpellTax(ai);
                                if (tax > 0)
                                {
                                    if (!cost.ContainsKey("Colorless"))
                                        cost["Colorless"] = 0;
                                    cost["Colorless"] += tax;
                                }
                                CardData artData = CardDatabase.GetCardData(card.cardName);
                                int reduction = (artData != null && artData.subtypes.Contains("Potion"))
                                    ? GameManager.Instance.GetPotionCostReduction(ai) : 0;
                                if (reduction > 0 && cost.ContainsKey("Colorless"))
                                    cost["Colorless"] = Mathf.Max(0, cost["Colorless"] - reduction);
                                if (EnsureManaForCost(ai, cost))
                                {
                                    ai.ColoredMana.Pay(cost);
                                    if (card.hasXCost)
                                    {
                                        card.xValue = ai.ColoredMana.Total();
                                        if (card.xValue > 0)
                                            ai.ColoredMana.SpendGeneric(card.xValue);
                                    }
                                    ai.Hand.Remove(card);
                                    ai.Battlefield.Add(card);
                                    card.OnEnterPlay(ai);
                                    GameManager.Instance.NotifyCreatureEntered(card, ai);
                                    GameManager.Instance.NotifyArtifactEntered(card, ai);

                                    if (card.entersTapped || GameManager.Instance.IsAllPermanentsEnterTappedActive())
                                    {
                                        card.isTapped = true;
                                        Debug.Log($"{card.cardName} (AI) enters tapped (static effect or base).");
                                    }

                                    GameObject obj = GameObject.Instantiate(GameManager.Instance.cardPrefab, GameManager.Instance.aiArtifactArea);
                                    CardVisual visual = obj.GetComponent<CardVisual>();
                                    visual.Setup(card, GameManager.Instance);
                                    visual.isInBattlefield = true;
                                    GameManager.Instance.activeCardVisuals.Add(visual);

                                    Debug.Log($"AI played artifact: {card.cardName}");
                                    playedCard = true;

                                    waitingForAIAction = true;
                                    StartCoroutine(WaitForAIAction(1f));
                                    return;
                                }
                            }
                            else if (card is AuraCard auraCard)
                            {
                                var cost = GameManager.Instance.GetManaCostBreakdown(auraCard.manaCost, auraCard.color);
                                int tax = GameManager.Instance.GetOpponentSpellTax(ai);
                                if (tax > 0)
                                {
                                    if (!cost.ContainsKey("Colorless"))
                                        cost["Colorless"] = 0;
                                    cost["Colorless"] += tax;
                                }

                                var potential = GetPotentialManaPool(ai);
                                if (!potential.CanPay(cost))
                                    continue;

                                CreatureCard target;
                                if (auraCard.buffPower >= 0 && auraCard.buffToughness >= 0)
                                    target = ai.Battlefield.OfType<CreatureCard>()
                                        .FirstOrDefault(c => auraCard.requiredTargetType != SorceryCard.TargetType.TappedCreature || c.isTapped);
                                else
                                    target = GameManager.Instance.GetOpponentOf(ai).Battlefield.OfType<CreatureCard>()
                                        .FirstOrDefault(c => auraCard.requiredTargetType != SorceryCard.TargetType.TappedCreature || c.isTapped);

                                if (target == null)
                                    continue;

                                if (!EnsureManaForCost(ai, cost))
                                    continue;

                                ai.ColoredMana.Pay(cost);
                                ai.Hand.Remove(card);
                                auraCard.attachedTo = target;
                                auraCard.owner = ai;
                                ai.Battlefield.Add(auraCard);
                                auraCard.OnEnterPlay(ai);
                                GameManager.Instance.NotifyEnchantmentEntered(auraCard, ai);

                                // Aura or enchanted creature might die upon entry
                                if (!ai.Battlefield.Contains(auraCard))
                                {
                                    waitingForAIAction = true;
                                    StartCoroutine(WaitForAIAction(1f));
                                    return;
                                }

                                bool auraSurvived = ai.Battlefield.Contains(auraCard);

                                if (auraSurvived)
                                {
                                    if (auraCard.entersTapped || GameManager.Instance.IsAllPermanentsEnterTappedActive())
                                    {
                                        auraCard.isTapped = true;
                                        Debug.Log($"{auraCard.cardName} (AI) enters tapped (static effect or base).");
                                    }

                                    GameObject obj = GameObject.Instantiate(GameManager.Instance.cardPrefab, GameManager.Instance.aiEnchantmentArea);
                                    CardVisual visual = obj.GetComponent<CardVisual>();
                                    visual.Setup(auraCard, GameManager.Instance);
                                    visual.isInBattlefield = true;
                                    GameManager.Instance.activeCardVisuals.Add(visual);
                                }

                                Debug.Log($"AI played aura: {card.cardName}");
                                playedCard = true;

                                waitingForAIAction = true;
                                StartCoroutine(WaitForAIAction(1f));
                                return;
                            }
                            else if (card is EnchantmentCard enchantment)
                            {
                                var cost = GameManager.Instance.GetManaCostBreakdown(enchantment.manaCost, enchantment.color);
                                int tax = GameManager.Instance.GetOpponentSpellTax(ai);
                                if (tax > 0)
                                {
                                    if (!cost.ContainsKey("Colorless"))
                                        cost["Colorless"] = 0;
                                    cost["Colorless"] += tax;
                                }
                                if (EnsureManaForCost(ai, cost))
                                {
                                    ai.ColoredMana.Pay(cost);
                                    if (card.hasXCost)
                                    {
                                        card.xValue = ai.ColoredMana.Total();
                                        if (card.xValue > 0)
                                            ai.ColoredMana.SpendGeneric(card.xValue);
                                    }
                                    ai.Hand.Remove(card);
                                    ai.Battlefield.Add(card);
                                    card.OnEnterPlay(ai);
                                    GameManager.Instance.NotifyEnchantmentEntered(card, ai);

                                    if (card.entersTapped || GameManager.Instance.IsAllPermanentsEnterTappedActive())
                                    {
                                        card.isTapped = true;
                                        Debug.Log($"{card.cardName} (AI) enters tapped (static effect or base).");
                                    }

                                    GameObject obj = GameObject.Instantiate(GameManager.Instance.cardPrefab, GameManager.Instance.aiEnchantmentArea);
                                    CardVisual visual = obj.GetComponent<CardVisual>();
                                    visual.Setup(card, GameManager.Instance);
                                    visual.isInBattlefield = true;
                                    GameManager.Instance.activeCardVisuals.Add(visual);

                                    Debug.Log($"AI played enchantment: {card.cardName}");
                                    playedCard = true;

                                    waitingForAIAction = true;
                                    StartCoroutine(WaitForAIAction(1f));
                                    return;
                                }
                            }
                        }
                    }

                    foreach (var card in ai.Battlefield)
                    {
                        if (card is CreatureCard creature &&
                            !creature.isTapped &&
                            (!creature.hasSummoningSickness || creature.keywordAbilities.Contains(KeywordAbility.Haste)))
                        {
                            // TAP TO LOSE LIFE
                            if (creature.activatedAbilities.Contains(ActivatedAbility.TapToLoseLife))
                            {
                                creature.isTapped = true;
                                GameManager.Instance.QueueCreatureActivatedAbility(creature, ActivatedAbility.TapToLoseLife, ai);
                                GameManager.Instance.FindCardVisual(creature)?.UpdateVisual();
                            }

                            // TAP TO CREATE MINER
                            if (creature.activatedAbilities.Contains(ActivatedAbility.TapToCreateToken))
                            {
                                int cost = creature.manaToPayToActivate;

                                if (EnsureManaForCost(ai, new Dictionary<string, int> { { "Colorless", cost } }))
                                {
                                    ai.ColoredMana.Pay(new Dictionary<string, int> { { "Colorless", cost } });

                                    creature.isTapped = true;

                                    GameManager.Instance.QueueCreatureActivatedAbility(creature, ActivatedAbility.TapToCreateToken, ai);
                                    GameManager.Instance.FindCardVisual(creature)?.UpdateVisual();
                                }
                                else
                                {
                                    Debug.Log($"AI can't create token — not enough total mana ({ai.ColoredMana.Total()}/{cost}).");
                                }
                            }

                            if (creature.activatedAbilities.Contains(ActivatedAbility.PayToGainAbility) &&
                                !creature.keywordAbilities.Contains(creature.abilityToGain))
                            {
                                int cost = creature.manaToPayToActivate;
                                string color = creature.PrimaryColor;
                                var abilityCost = new Dictionary<string, int>();

                                if (!string.IsNullOrEmpty(color) && color != "Artifact")
                                {
                                    abilityCost[color] = 1;
                                    if (cost > 1)
                                        abilityCost["Colorless"] = cost - 1;
                                }
                                else
                                {
                                    abilityCost["Colorless"] = cost;
                                }

                                if (EnsureManaForCost(ai, abilityCost))
                                {
                                    GameManager.Instance.QueueCreatureActivatedAbility(creature, ActivatedAbility.PayToGainAbility, ai);
                                    GameManager.Instance.FindCardVisual(creature)?.UpdateVisual();
                                }
                            }
                        }
                    }

                    foreach (var card in ai.Battlefield.ToList()) // .ToList() because we may remove during iteration
                    {
                        if (GameManager.Instance.IsStackActive())
                            break;

                        if (card is ArtifactCard artifact && !artifact.isTapped)
                        {
                            if (artifact.activatedAbilities.Contains(ActivatedAbility.TapToGainLife))
                            {
                                artifact.isTapped = true;
                                GameManager.Instance.QueueArtifactActivatedAbility(artifact, ActivatedAbility.TapToGainLife, ai);
                                Debug.Log($"AI taps {artifact.cardName} to gain 1 life.");
                                GameManager.Instance.FindCardVisual(artifact)?.UpdateVisual();
                                GameManager.Instance.UpdateUI();
                            }
                            else if (artifact.activatedAbilities.Contains(ActivatedAbility.TapToPlague))
                            {
                                artifact.isTapped = true;
                                GameManager.Instance.QueueArtifactActivatedAbility(artifact, ActivatedAbility.TapToPlague, ai);
                                GameManager.Instance.FindCardVisual(artifact)?.UpdateVisual();
                                GameManager.Instance.UpdateUI();
                            }
                            else if (artifact.activatedAbilities.Contains(ActivatedAbility.SacrificeForLife))
                            {
                                var abilityCost = new Dictionary<string, int> { { "Colorless", artifact.manaToPayToActivate } };
                                if (EnsureManaForCost(ai, abilityCost))
                                {
                                    ai.ColoredMana.Pay(abilityCost);
                                    artifact.isTapped = true;
                                    GameManager.Instance.SendToGraveyard(artifact, ai);
                                    GameManager.Instance.QueueArtifactActivatedAbility(artifact, ActivatedAbility.SacrificeForLife, ai);
                                    GameManager.Instance.FindCardVisual(artifact)?.UpdateVisual();
                                    GameManager.Instance.UpdateUI();
                                }
                            }
                            else if (artifact.activatedAbilities.Contains(ActivatedAbility.SacrificeToDrawCards))
                            {
                                var abilityCost = new Dictionary<string, int> { { "Colorless", artifact.manaToPayToActivate } };
                                if (EnsureManaForCost(ai, abilityCost))
                                {
                                    ai.ColoredMana.Pay(abilityCost);
                                    artifact.isTapped = true;
                                    GameManager.Instance.SendToGraveyard(artifact, ai);
                                    GameManager.Instance.QueueArtifactActivatedAbility(artifact, ActivatedAbility.SacrificeToDrawCards, ai);
                                    GameManager.Instance.FindCardVisual(artifact)?.UpdateVisual();
                                    GameManager.Instance.UpdateUI();
                                }
                            }
                            else if (artifact.activatedAbilities.Contains(ActivatedAbility.TapToPlayRandomPotion))
                            {
                                var abilityCost = new Dictionary<string, int> { { "Colorless", artifact.manaToPayToActivate } };
                                if (EnsureManaForCost(ai, abilityCost))
                                {
                                    ai.ColoredMana.Pay(abilityCost);
                                    artifact.isTapped = true;
                                    GameManager.Instance.QueueArtifactActivatedAbility(artifact, ActivatedAbility.TapToPlayRandomPotion, ai);
                                    GameManager.Instance.FindCardVisual(artifact)?.UpdateVisual();
                                    GameManager.Instance.UpdateUI();
                                }
                            }
                        }
                    }

                    GameManager.Instance.UpdateUI(); // update UI after all actions
                    if (GameManager.Instance.IsStackActive())
                    {
                        Debug.Log("AI cast a sorcery — waiting... will not advance phase until resolved.");
                        return; // just wait
                    }

                    AdvancePhase();
                    break;
                }
                break;

            case TurnPhase.EnterCombat:
                Debug.Log("→ Entering Combat.");
                AdvancePhase();
                break;

            case TurnPhase.ChooseAttackers:
                if (currentPlayer == PlayerType.Human)
                {
                    Debug.Log("→ Choose attackers.");
                    attackAllButton.gameObject.SetActive(true);
                    clearAttackersButton.gameObject.SetActive(true);
                    waitingForPlayerInput = true;
                    confirmAttackersButton.gameObject.SetActive(true);
                }
                else
                {
                    Debug.Log("→ AI chooses attackers.");
                    GameManager.Instance.currentAttackers.Clear();

                    Player ai = GameManager.Instance.aiPlayer;
                    Player human = GameManager.Instance.humanPlayer;

                    var potentialAttackers = new List<CreatureCard>();

                    foreach (var card in ai.Battlefield)
                    {
                        if (card is CreatureCard creature &&
                            !creature.isTapped &&
                            !creature.hasSummoningSickness &&
                            !creature.keywordAbilities.Contains(KeywordAbility.Defender))
                        {
                            potentialAttackers.Add(creature);
                        }
                    }

                    int totalPower = potentialAttackers.Sum(c => c.power);

                    bool goForLethal = totalPower >= human.Life;

                    const int lowLifeThreshold = 5;
                    bool lowLifeNeedsDefense = ai.Life <= lowLifeThreshold &&
                        human.Battlefield.OfType<CreatureCard>().Any();

                    foreach (var creature in potentialAttackers)
                    {
                        bool attack = ShouldAIAttackCreature(creature, ai, human, goForLethal, lowLifeNeedsDefense);

                        if (attack)
                        {
                            if (!creature.keywordAbilities.Contains(KeywordAbility.Vigilance))
                                creature.isTapped = true;

                            GameManager.Instance.currentAttackers.Add(creature);
                            GameManager.Instance.FindCardVisual(creature)?.swordIcon?.SetActive(true);
                            Debug.Log($"AI declares attacker: {creature.cardName}");
                            GameManager.Instance.FindCardVisual(creature)?.UpdateVisual();
                        }
                    }

                    AdvancePhase(); // skip straight to ConfirmAttackers (AI skips confirm)
                }
                break;

            case TurnPhase.ConfirmAttackers:
                if (currentPlayer == PlayerType.Human)
                {
                    if (waitingForPlayerInput)
                    {
                        Debug.Log("→ Confirm attackers.");
                        confirmAttackersButton.gameObject.SetActive(true);
                    }
                    else
                    {
                        AdvancePhase();
                    }
                }
                else
                {
                    Debug.Log("→ Skipping attacker confirmation (AI turn).");
                    AdvancePhase();
                }
                break;

            case TurnPhase.ChooseBlockers:
                if (GameManager.Instance.currentAttackers.Count == 0)
                {
                    Debug.Log("→ No attackers. Skipping combat.");
                    GameManager.Instance.currentAttackers.Clear();
                    waitingForPlayerInput = false;
                    confirmBlockersButton.gameObject.SetActive(false);
                    RunSpecificPhase(TurnPhase.Main2);
                    break;
                }

                if (currentPlayer == PlayerType.Human)
                {
                    Debug.Log("→ AI is assigning blockers as defender.");

                    Player ai = GameManager.Instance.aiPlayer;
                    Player human = GameManager.Instance.humanPlayer;
                    var attackers = GameManager.Instance.currentAttackers.OrderByDescending(a => a.power).ToList();
                    var availableBlockers = new List<CreatureCard>();
                    int remainingDamage = attackers.Sum(a => a.power);
                    int projectedLife = ai.Life;

                    // Gather untapped blockers that can block
                    foreach (var card in ai.Battlefield)
                    {
                        if (card is CreatureCard c &&
                            !c.isTapped &&
                            !c.keywordAbilities.Contains(KeywordAbility.CantBlock))
                        {
                            availableBlockers.Add(c);
                        }
                    }

                    // Assign blockers to attackers prioritizing survival/trades
                    foreach (var attacker in attackers)
                    {
                        var chosenBlockers = ChooseBestBlockers(attacker, availableBlockers, projectedLife, remainingDamage);

                        if (chosenBlockers != null && chosenBlockers.Count > 0)
                        {
                            if (!GameManager.Instance.blockingAssignments.ContainsKey(attacker))
                                GameManager.Instance.blockingAssignments[attacker] = new List<CreatureCard>();

                            foreach (var blocker in chosenBlockers)
                            {
                                GameManager.Instance.blockingAssignments[attacker].Add(blocker);
                                blocker.blockingThisAttacker = attacker;
                                attacker.blockedByThisBlocker.Add(blocker);
                                availableBlockers.Remove(blocker);
                                Debug.Log($"AI blocks {attacker.cardName} with {blocker.cardName}");
                            }

                            remainingDamage -= attacker.power;
                        }
                        else
                        {
                            projectedLife -= attacker.power;
                            remainingDamage -= attacker.power;
                        }
                    }

                    AdvancePhase(); // Proceed to ConfirmBlockers (or Damage)
                }
                else
                {
                    Debug.Log("→ Player chooses blockers.");
                    waitingForPlayerInput = true;
                    confirmBlockersButton.gameObject.SetActive(true);
                }
                break;

            case TurnPhase.ConfirmBlockers:
                foreach (var attacker in GameManager.Instance.currentAttackers)
                {
                    foreach (var blocker in attacker.blockedByThisBlocker)
                    {
                        GameManager.Instance.NotifyCreatureBlocks(blocker, attacker);
                    }
                }

                if (!waitingForPlayerInput)
                {
                    waitingForPlayerInput = true;
                    if (currentPlayer == PlayerType.AI)
                        confirmBlockersButton.gameObject.SetActive(true);
                    Debug.Log("→ Blockers declared. Awaiting confirmation.");
                }
                break;

            case TurnPhase.Damage:
                Debug.Log("→ Resolving combat damage.");
                if (damageCoroutine == null)
                    damageCoroutine = StartCoroutine(WaitToShowCombatDamage());
                break;

            case TurnPhase.EndTurn:
                Debug.Log("→ Ending turn.");

                // Heal all creatures
                GameManager.Instance.ResetDamage(GameManager.Instance.humanPlayer);
                GameManager.Instance.ResetDamage(GameManager.Instance.aiPlayer);

                // Reference the correct player before swapping turn
                Player endingPlayer = currentPlayer == PlayerType.Human
                    ? GameManager.Instance.humanPlayer
                    : GameManager.Instance.aiPlayer;
                Player otherPlayer = endingPlayer == GameManager.Instance.humanPlayer
                    ? GameManager.Instance.aiPlayer
                    : GameManager.Instance.humanPlayer;

                foreach (var thisplayer in new Player[] { endingPlayer, otherPlayer })
                {
                    // Remove temporary keyword abilities and buffs
                    foreach (var card in thisplayer.Battlefield)
                    {
                        if (card is CreatureCard creature)
                        {
                            if (creature.temporaryKeywordAbilities.Count > 0)
                            {
                                foreach (var temp in new List<KeywordAbility>(creature.temporaryKeywordAbilities))
                                {
                                    while (creature.keywordAbilities.Contains(temp))
                                    {
                                        creature.keywordAbilities.Remove(temp);
                                        Debug.Log($"{creature.cardName} loses {temp} at end of turn.");
                                    }
                                }

                                creature.temporaryKeywordAbilities.Clear();

                                var visual = GameManager.Instance.FindCardVisual(card);
                                if (visual != null)
                                    visual.UpdateVisual();
                            }

                            if (creature.tempPowerBonus != 0 || creature.tempToughnessBonus != 0)
                            {
                                creature.ResetTemporaryBuff();
                                var visual = GameManager.Instance.FindCardVisual(card);
                                if (visual != null)
                                    visual.UpdateVisual();
                                Debug.Log($"{creature.cardName} loses temporary buff at end of turn.");
                            }
                        }
                    }
                }

                // Only after cleanup, begin next turn
                if (endingPlayer.extraTurns > 0)
                {
                    endingPlayer.extraTurns--;
                    Debug.Log($"{currentPlayer} gets an extra turn.");
                    BeginTurn(currentPlayer);
                }
                else
                {
                    BeginTurn(currentPlayer == PlayerType.Human ? PlayerType.AI : PlayerType.Human);
                }
                break;
        }
    }

    private IEnumerator HandleUpkeepTriggers(Player player)
    {
        foreach (var card in player.Battlefield.ToList())
        {
            foreach (var ability in card.abilities)
            {
                if (ability.timing == TriggerTiming.OnUpkeep && ability.effect != null)
                {
                    Debug.Log($"[Upkeep Trigger] {card.cardName} triggers OnUpkeep.");
                    GameManager.Instance.pendingStackEffects++;
                    yield return StartCoroutine(GameManager.Instance.ResolveTriggeredAbilityOnStack(ability, player, card, card));
                }
            }
        }

        AdvancePhase();
    }

    private IEnumerator WaitAndAdvancePhase()
    {
        yield return new WaitUntil(() => !GameManager.Instance.IsStackActive());

        // Wait a frame to ensure any triggered UI changes or effects have finished
        yield return null;

        if (!GameManager.Instance.gameOver)
            AdvancePhase(); // <-- This must always be called
    }

    private IEnumerator WaitForBannerAndStart()
    {
        // Previously this coroutine waited until the banner was manually
        // hidden somewhere else. If that never happened, the coroutine
        // would yield forever and the turn would never progress,
        // effectively preventing the player from ending their turn.

        // Show the banner for a short, fixed duration then hide it
        // ourselves to ensure the game always advances to the next
        // phase.
        yield return new WaitForSeconds(1f);

        if (turnBanner != null)
            turnBanner.SetActive(false);

        AdvancePhase();
    }

    private IEnumerator WaitForAIAction(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        waitingForAIAction = false;
    }

    private bool IsLandwalkPreventingBlock(CreatureCard attacker, Player defender)
    {
        foreach (var ability in attacker.keywordAbilities)
        {
            if (ability == KeywordAbility.Plainswalk &&
                defender.Battlefield.Any(card => card is LandCard land && land.cardName.ToLower().Contains("plains")))
                return true;
            if (ability == KeywordAbility.Islandwalk &&
                defender.Battlefield.Any(card => card is LandCard land && land.cardName.ToLower().Contains("island")))
                return true;
            if (ability == KeywordAbility.Swampwalk &&
                defender.Battlefield.Any(card => card is LandCard land && land.cardName.ToLower().Contains("swamp")))
                return true;
            if (ability == KeywordAbility.Mountainwalk &&
                defender.Battlefield.Any(card => card is LandCard land && land.cardName.ToLower().Contains("mountain")))
                return true;
            if (ability == KeywordAbility.Forestwalk &&
                defender.Battlefield.Any(card => card is LandCard land && land.cardName.ToLower().Contains("forest")))
                return true;
        }
        return false;
    }



    private bool BlockerCanBlockAttacker(CreatureCard blocker, CreatureCard attacker, Player defender)
    {
        if (blocker.isTapped) return false;
        if (blocker.keywordAbilities.Contains(KeywordAbility.CantBlock)) return false;

        if (attacker.keywordAbilities.Contains(KeywordAbility.Flying) &&
            !blocker.keywordAbilities.Contains(KeywordAbility.Flying) &&
            !blocker.keywordAbilities.Contains(KeywordAbility.Reach))
            return false;

        if (blocker.keywordAbilities.Contains(KeywordAbility.CanOnlyBlockFlying) &&
            !attacker.keywordAbilities.Contains(KeywordAbility.Flying))
            return false;

        if (IsLandwalkPreventingBlock(attacker, defender))
            return false;

        if (GameManager.Instance.IsHasteCreaturesOnlyBlockedByHasteActive(attacker.owner) &&
            attacker.keywordAbilities.Contains(KeywordAbility.Haste) &&
            !blocker.keywordAbilities.Contains(KeywordAbility.Haste))
            return false;

        if (blocker.color.Any(c => attacker.keywordAbilities.Contains(ProtectionUtils.GetProtectionKeyword(c))))
            return false;

        return true;
    }

    private CreatureCard ChooseBestBlocker(CreatureCard attacker, List<CreatureCard> candidates, int remainingLife, int remainingDamage)
    {
        var possible = candidates.Where(b => BlockerCanBlockAttacker(b, attacker, GameManager.Instance.aiPlayer)).ToList();
        if (possible.Count == 0) return null;

        // Prefer blocker that kills attacker and survives
        var killSurvive = possible
            .Where(b => !attacker.keywordAbilities.Contains(KeywordAbility.Indestructible) &&
                        b.power >= attacker.toughness &&
                        (b.toughness > attacker.power || b.keywordAbilities.Contains(KeywordAbility.Indestructible)))
            .OrderBy(b => b.power)
            .FirstOrDefault();
        if (killSurvive != null) return killSurvive;

        // Safe block that survives damage
        var safe = possible
            .Where(b => b.keywordAbilities.Contains(KeywordAbility.Indestructible) || b.toughness > attacker.power)
            .OrderBy(b => b.toughness)
            .FirstOrDefault();
        if (safe != null) return safe;

        // Trade if it can kill attacker
        var trade = possible
            .Where(b => !attacker.keywordAbilities.Contains(KeywordAbility.Indestructible) && b.power >= attacker.toughness)
            .OrderBy(b => b.power + b.baseToughness)
            .FirstOrDefault();
        if (trade != null)
        {
            int blockerValue = trade.power + trade.baseToughness;
            int attackerValue = attacker.power + attacker.baseToughness;
            if (attackerValue >= blockerValue || remainingDamage >= remainingLife)
                return trade;
        }

        // Block anything to avoid lethal damage
        if (remainingDamage >= remainingLife)
            return possible.OrderByDescending(b => b.toughness).First();

        return null;
    }

    private List<CreatureCard> ChooseBestBlockers(CreatureCard attacker, List<CreatureCard> candidates, int remainingLife, int remainingDamage)
    {
        var possible = candidates.Where(b => BlockerCanBlockAttacker(b, attacker, GameManager.Instance.aiPlayer)).ToList();
        if (possible.Count == 0)
            return null;

        // Prefer a single blocker that kills and survives
        var singleKill = possible
            .Where(b => !attacker.keywordAbilities.Contains(KeywordAbility.Indestructible) &&
                        b.power >= attacker.toughness &&
                        (b.toughness > attacker.power || b.keywordAbilities.Contains(KeywordAbility.Indestructible)))
            .OrderBy(b => b.power + b.baseToughness)
            .FirstOrDefault();
        if (singleKill != null)
            return new List<CreatureCard> { singleKill };

        // Look for multi-blocking groups that kill attacker without losing blockers
        var combos = GenerateBlockerCombinations(possible, 3);
        List<CreatureCard> bestNoLoss = null;
        int bestNoLossValue = int.MaxValue;
        foreach (var combo in combos)
        {
            var (kills, casualties, valueLost) = EvaluateBlockerCombo(attacker, combo);
            if (kills && casualties == 0)
            {
                int val = combo.Sum(b => b.power + b.baseToughness);
                if (val < bestNoLossValue)
                {
                    bestNoLossValue = val;
                    bestNoLoss = combo;
                }
            }
        }
        if (bestNoLoss != null)
            return bestNoLoss;

        // Fall back to single blocker heuristics (may be safe block or trade)
        var single = ChooseBestBlocker(attacker, possible, remainingLife, remainingDamage);
        if (single != null && single.power >= attacker.toughness &&
            !attacker.keywordAbilities.Contains(KeywordAbility.Indestructible))
            return new List<CreatureCard> { single };
        if (single != null && (single.toughness > attacker.power ||
                               single.keywordAbilities.Contains(KeywordAbility.Indestructible)))
            return new List<CreatureCard> { single };

        // Try to kill with multiple blockers even if some may die
        List<CreatureCard> bestTrade = null;
        int bestTradeValue = int.MaxValue;
        int attackerValue = attacker.power + attacker.baseToughness;
        foreach (var combo in combos)
        {
            var (kills, casualties, valueLost) = EvaluateBlockerCombo(attacker, combo);
            if (!kills)
                continue;

            if (valueLost <= attackerValue || remainingDamage >= remainingLife)
            {
                if (valueLost < bestTradeValue)
                {
                    bestTradeValue = valueLost;
                    bestTrade = combo;
                }
            }
        }
        if (bestTrade != null)
            return bestTrade;

        // Chump block to avoid lethal damage
        if (remainingDamage >= remainingLife)
            return new List<CreatureCard> { possible.OrderByDescending(b => b.toughness).First() };

        return null;
    }

    private (bool killsAttacker, int casualties, int valueLost) EvaluateBlockerCombo(CreatureCard attacker, List<CreatureCard> blockers)
    {
        int totalPower = blockers.Sum(b => b.power);
        bool blockersHaveDeathtouch = blockers.Any(b => b.keywordAbilities.Contains(KeywordAbility.Deathtouch));
        bool attackerHasDeathtouch = attacker.keywordAbilities.Contains(KeywordAbility.Deathtouch);
        bool attackerHasIndestructible = attacker.keywordAbilities.Contains(KeywordAbility.Indestructible);

        bool killsAttacker = !attackerHasIndestructible && (totalPower >= attacker.toughness || blockersHaveDeathtouch);

        int remainingDamage = attacker.power;
        int casualties = 0;
        int valueLost = 0;
        var orderedBlockers = blockers.OrderBy(x => x.toughness).ToList();
        bool attackerHasTrample = attacker.keywordAbilities.Contains(KeywordAbility.Trample);
        for (int i = 0; i < orderedBlockers.Count && remainingDamage > 0; i++)
        {
            var b = orderedBlockers[i];

            int damage;
            bool isLastBlocker = i == orderedBlockers.Count - 1;
            if (!attackerHasTrample && isLastBlocker)
                damage = remainingDamage;
            else
                damage = Mathf.Min(remainingDamage, b.toughness);

            if (attackerHasDeathtouch && remainingDamage > 0)
                damage = b.toughness;

            bool blockerDies = damage >= b.toughness && !b.keywordAbilities.Contains(KeywordAbility.Indestructible);
            if (blockerDies)
            {
                casualties++;
                valueLost += b.power + b.baseToughness;
            }
            remainingDamage -= damage;
        }

        return (killsAttacker, casualties, valueLost);
    }

    private IEnumerable<List<CreatureCard>> GenerateBlockerCombinations(List<CreatureCard> cards, int maxSize)
    {
        List<List<CreatureCard>> results = new List<List<CreatureCard>>();

        void Recurse(int start, int size, List<CreatureCard> current)
        {
            if (current.Count == size)
            {
                results.Add(new List<CreatureCard>(current));
                return;
            }
            for (int i = start; i < cards.Count; i++)
            {
                current.Add(cards[i]);
                Recurse(i + 1, size, current);
                current.RemoveAt(current.Count - 1);
            }
        }

        int limit = Mathf.Min(maxSize, cards.Count);
        for (int s = 2; s <= limit; s++)
            Recurse(0, s, new List<CreatureCard>());

        return results;
    }

    private bool ShouldAIAttackCreature(CreatureCard creature, Player ai, Player human, bool goForLethal, bool lowLifeNeedsDefense)
    {
        // Avoid attacking with creatures that cannot deal damage
        if (creature.power <= 0)
            return false;

        if (goForLethal)
            return true;

        var possibleBlockers = human.Battlefield
            .OfType<CreatureCard>()
            .Where(b => BlockerCanBlockAttacker(b, creature, human) &&
                        !b.isTapped &&
                        !b.keywordAbilities.Contains(KeywordAbility.CantBlock))
            .OrderByDescending(b => b.power + b.baseToughness)
            .ToList();

        // Creatures that cannot block should attack unless a blocker can kill them and survive
        if (creature.keywordAbilities.Contains(KeywordAbility.CantBlock))
        {
            if (possibleBlockers.Count == 0)
                return true;

            var bestCantBlock = possibleBlockers.First();
            bool blockerKillsAndSurvivesCantBlock = !creature.keywordAbilities.Contains(KeywordAbility.Indestructible) &&
                bestCantBlock.power >= creature.toughness &&
                (bestCantBlock.toughness > creature.power || bestCantBlock.keywordAbilities.Contains(KeywordAbility.Indestructible));

            return !blockerKillsAndSurvivesCantBlock;
        }

        if (lowLifeNeedsDefense && !creature.keywordAbilities.Contains(KeywordAbility.Vigilance))
            return false;

        if (possibleBlockers.Count == 0)
            return true;

        var best = possibleBlockers.First();

        bool blockerKillsAndSurvives = !creature.keywordAbilities.Contains(KeywordAbility.Indestructible) &&
            best.power >= creature.toughness &&
            (best.toughness > creature.power || best.keywordAbilities.Contains(KeywordAbility.Indestructible));
        if (blockerKillsAndSurvives && !goForLethal)
            return false;

        int creatureValue = creature.power + creature.baseToughness;
        int blockerValue = best.power + best.baseToughness;
        bool tradeUp = creature.power >= best.toughness && creatureValue <= blockerValue &&
                       !best.keywordAbilities.Contains(KeywordAbility.Indestructible);
        bool aggressive = ai.Life >= human.Life;

        return tradeUp || goForLethal || (aggressive && creatureValue >= blockerValue);
    }

    public void ContinueAIAfterStack()
    {
        if (currentPlayer == PlayerType.AI)
        {
            Debug.Log("AI stack resolved — resuming AI turn.");
            RunCurrentPhase();
        }
    }

    public void RunSpecificPhase(TurnPhase phase)
    {
        currentPhase = phase;
        RunCurrentPhase();
    }


    private IEnumerator WaitToShowCombatDamage()
    {
        VisualEffectManager.Instance.DeferLifeDeltaFade(true);
        yield return StartCoroutine(GameManager.Instance.ResolveCombatWithAnimations());
        yield return new WaitUntil(() => GameManager.Instance.pendingGraveyardAnimations == 0);

        GameManager.Instance.CheckForGameEnd();

        foreach (var visual in GameManager.Instance.activeCardVisuals)
        {
            if (visual.swordIcon != null)
                visual.swordIcon.SetActive(false);
            if (visual.shieldIcon != null)
                visual.shieldIcon.SetActive(false);

            visual.UpdateVisual();
        }

        VisualEffectManager.Instance.DeferLifeDeltaFade(false);
        VisualEffectManager.Instance.FinalizeLifeDeltas();

        AdvancePhase();


        damageCoroutine = null;
    }

    private Player.ManaPool GetPotentialManaPool(Player ai)
    {
        Player.ManaPool pool = new Player.ManaPool();
        pool.White = ai.ColoredMana.White;
        pool.Blue = ai.ColoredMana.Blue;
        pool.Black = ai.ColoredMana.Black;
        pool.Red = ai.ColoredMana.Red;
        pool.Green = ai.ColoredMana.Green;
        pool.Colorless = ai.ColoredMana.Colorless;

        foreach (var card in ai.Battlefield)
        {
            if (card is LandCard land && !land.isTapped)
            {
                var colors = CardDatabase.GetCardData(land.cardName).color;
                string color = (colors != null && colors.Count > 0) ? colors[0] : "Colorless";
                switch (color)
                {
                    case "White": pool.White++; break;
                    case "Blue": pool.Blue++; break;
                    case "Black": pool.Black++; break;
                    case "Red": pool.Red++; break;
                    case "Green": pool.Green++; break;
                    default: pool.Colorless++; break;
                }
            }
            else if (!card.isTapped && (card is CreatureCard c && c.activatedAbilities.Contains(ActivatedAbility.TapForMana)))
            {
                pool.Colorless++;
            }
            else if (!card.isTapped && (card is ArtifactCard a && a.activatedAbilities.Contains(ActivatedAbility.TapForMana)))
            {
                pool.Colorless++;
            }
            else if (!card.isTapped && (card is ArtifactCard a2 && a2.activatedAbilities.Contains(ActivatedAbility.TapAndSacrificeForMana)))
            {
                pool.Colorless++;
            }
        }

        return pool;
    }

    private bool TapLandForColor(Player ai, string color)
    {
        foreach (var card in ai.Battlefield)
        {
            if (card is LandCard land && !land.isTapped)
            {
                var colors = CardDatabase.GetCardData(land.cardName).color;
                string landColor = (colors != null && colors.Count > 0) ? colors[0] : "Colorless";
                if (landColor == color)
                {
                    GameManager.Instance.TapLandForMana(land, ai);
                    GameManager.Instance.FindCardVisual(land)?.UpdateVisual();
                    return true;
                }
            }
        }
        return false;
    }

    private bool TapAnyManaSource(Player ai)
    {
        foreach (var card in ai.Battlefield)
        {
            if (card is LandCard land && !land.isTapped)
            {
                GameManager.Instance.TapLandForMana(land, ai);
                GameManager.Instance.FindCardVisual(land)?.UpdateVisual();
                return true;
            }
        }

        foreach (var card in ai.Battlefield)
        {
            if (card is CreatureCard creature && !creature.isTapped && creature.activatedAbilities.Contains(ActivatedAbility.TapForMana))
            {
                creature.isTapped = true;
                ai.ColoredMana.Colorless += 1;
                GameManager.Instance.FindCardVisual(creature)?.UpdateVisual();
                return true;
            }
            if (card is ArtifactCard artifact && !artifact.isTapped && artifact.activatedAbilities.Contains(ActivatedAbility.TapForMana))
            {
                artifact.isTapped = true;
                ai.ColoredMana.Colorless += 1;
                GameManager.Instance.FindCardVisual(artifact)?.UpdateVisual();
                return true;
            }
            if (card is ArtifactCard artifact2 && !artifact2.isTapped && artifact2.activatedAbilities.Contains(ActivatedAbility.TapAndSacrificeForMana))
            {
                artifact2.isTapped = true;
                ai.ColoredMana.Colorless += 1;
                GameManager.Instance.SendToGraveyard(artifact2, ai);
                GameManager.Instance.FindCardVisual(artifact2)?.UpdateVisual();
                return true;
            }
        }

        return false;
    }

    private bool EnsureManaForCost(Player ai, Dictionary<string, int> cost)
    {
        var potential = GetPotentialManaPool(ai);
        if (!potential.CanPay(cost))
            return false;

        int needWhite = cost.ContainsKey("White") ? cost["White"] : 0;
        while (ai.ColoredMana.White < needWhite)
        {
            if (!TapLandForColor(ai, "White")) return false;
        }

        int needBlue = cost.ContainsKey("Blue") ? cost["Blue"] : 0;
        while (ai.ColoredMana.Blue < needBlue)
        {
            if (!TapLandForColor(ai, "Blue")) return false;
        }

        int needBlack = cost.ContainsKey("Black") ? cost["Black"] : 0;
        while (ai.ColoredMana.Black < needBlack)
        {
            if (!TapLandForColor(ai, "Black")) return false;
        }

        int needRed = cost.ContainsKey("Red") ? cost["Red"] : 0;
        while (ai.ColoredMana.Red < needRed)
        {
            if (!TapLandForColor(ai, "Red")) return false;
        }

        int needGreen = cost.ContainsKey("Green") ? cost["Green"] : 0;
        while (ai.ColoredMana.Green < needGreen)
        {
            if (!TapLandForColor(ai, "Green")) return false;
        }

        int totalCost = cost.Values.Sum();
        while (ai.ColoredMana.Total() < totalCost)
        {
            if (!TapAnyManaSource(ai))
                break;
        }

        return ai.ColoredMana.CanPay(cost);
    }

    public void SelectAllEligibleAttackers()
    {
        if (GameManager.Instance.graveyardViewActive)
            return;

        // Clear any previously selected attackers so the function is idempotent
        // This prevents creatures from staying tapped without being
        // registered as attackers if the button is pressed multiple
        // times in the same combat step
        ClearAllSelectedAttackers();

        bool anyDeclared = false;

        foreach (var card in GameManager.Instance.humanPlayer.Battlefield)
        {
            if (card is CreatureCard creature &&
                !creature.isTapped &&
                (!creature.hasSummoningSickness || creature.keywordAbilities.Contains(KeywordAbility.Haste)) &&
                !creature.keywordAbilities.Contains(KeywordAbility.Defender))
            {
                GameManager.Instance.selectedAttackers.Add(creature);
                anyDeclared = true;

                // Tap the creature unless it has Vigilance
                if (!creature.keywordAbilities.Contains(KeywordAbility.Vigilance))
                    creature.isTapped = true;

                var visual = GameManager.Instance.FindCardVisual(creature);
                if (visual != null)
                {
                    if (visual.swordIcon != null)
                        visual.swordIcon.SetActive(true);
                    visual.UpdateVisual();
                }
            }
        }

        if (anyDeclared)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.declareAttack); // Or use .attack if that's your clip name
        }
    }

    public void ClearAllSelectedAttackers()
    {
        if (GameManager.Instance.graveyardViewActive)
            return;

        foreach (var creature in GameManager.Instance.selectedAttackers)
        {
            creature.isTapped = false;
        }

        // Now clear the list BEFORE updating visuals
        var toClear = GameManager.Instance.selectedAttackers.ToList();
        GameManager.Instance.selectedAttackers.Clear();

        foreach (var creature in toClear)
        {
            var visual = GameManager.Instance.FindCardVisual(creature);
            if (visual != null)
            {
                if (visual.swordIcon != null)
                    visual.swordIcon.SetActive(false);

                visual.UpdateVisual();
            }
        }
    }
}