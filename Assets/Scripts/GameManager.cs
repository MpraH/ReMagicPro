using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    // Temporarily stores the player's selected blocker creature during the
    // Choose Blockers phase. The player first selects their own creature and
    // then clicks an attacking enemy creature to assign the block.
    public CreatureCard selectedBlockerForBlocking = null;

    public Player humanPlayer;
    public Player aiPlayer;
    public TMP_Text manaPoolText;
    public TMP_Text playerLifeText;
    public TMP_Text enemyLifeText;
    public TMP_Text enemyHandText;
    public TMP_Text playerDeckCountText;
    public TMP_Text playerGraveyardCountText;
    public TMP_Text enemyDeckCountText;
    public TMP_Text enemyGraveyardCountText;

    public Transform playerHandArea;
    public Transform playerBattlefieldArea;
    public Transform playerGraveyardArea;
    public Transform playerLandArea;
    public Transform playerArtifactArea;
    public Transform playerEnchantmentArea;

    public Transform stackZone; //shared zone

    public Transform aiBattlefieldArea;
    public Transform aiGraveyardArea;
    public Transform aiLandArea;
    public Transform aiArtifactArea;
    public Transform aiEnchantmentArea;

    public GameObject cardPrefab;
    public GameObject manaVFXPrefab;
    public GameObject bloodSplatPrefab;
    public GameObject deathPlaceholderPrefab;
    public GameObject artifactDeathPrefab;
    public GameObject triggerVFXPrefab;

    public ArtifactCard targetingArtifact;
    public EquipmentCard targetingEquipment;

    public Sprite blueIcon, whiteIcon, blackIcon, redIcon, greenIcon;

    public Image whiteManaIcon;
    public TMP_Text whiteManaText;
    public Image blueManaIcon;
    public TMP_Text blueManaText;
    public Image blackManaIcon;
    public TMP_Text blackManaText;
    public Image redManaIcon;
    public TMP_Text redManaText;
    public Image greenManaIcon;
    public TMP_Text greenManaText;
    public Image colorlessManaIcon;
    public TMP_Text colorlessManaText;

    // Name of the player's favourite card selected in the deck editor
    private string favouriteCardName;

    public List<CardVisual> activeCardVisuals = new List<CardVisual>();
    public List<CreatureCard> selectedAttackers = new List<CreatureCard>();
    public List<CreatureCard> currentAttackers = new List<CreatureCard>();

    public Dictionary<CreatureCard, List<CreatureCard>> blockingAssignments = new Dictionary<CreatureCard, List<CreatureCard>>();

    public bool isStackBusy = false;
    public int pendingStackEffects = 0;
    public bool gameOver = false;
    public int pendingGraveyardAnimations = 0;
    public bool graveyardViewActive = false;
    public GraveyardUIManager graveyardUIManager;

    // Tracks cards already moved to the graveyard this turn to
    // prevent duplicate death triggers if CheckDeaths runs again.
    private HashSet<Card> processedDeaths = new HashSet<Card>();

    public SorceryCard targetingSorcery;
    public AuraCard targetingAura;
    public Player targetingPlayer;
    public CardVisual targetingVisual;
    public bool isTargetingMode = false;

    public Card targetingCreature;
    public CardAbility targetingAbility;
    public Card targetingCreatureOptional;
    public CardAbility optionalAbility;
    public Player optionalTargetPlayer;

    private struct TriggeredAbilityContext
    {
        public CardAbility ability;
        public Player owner;
        public Card source;
        public Card target;
        public Card deadCreature;

        public TriggeredAbilityContext(CardAbility ability, Player owner, Card source, Card target, Card deadCreature)
        {
            this.ability = ability;
            this.owner = owner;
            this.source = source;
            this.target = target;
            this.deadCreature = deadCreature;
        }
    }

    private readonly Queue<TriggeredAbilityContext> triggerQueue = new Queue<TriggeredAbilityContext>();
    private bool processingTriggerQueue = false;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public bool IsStackActive()
    {
        return isStackBusy || pendingStackEffects > 0;
    }

    void Start()
    {
        humanPlayer = new Player();
        aiPlayer = new Player();

        // Load favourite card from deck editor if one was set
        favouriteCardName = DeckHolder.FavouriteCardName;

        Debug.Log("Loading deck for zone ID: " + BattleData.CurrentZoneId);

        DeckDatabase.BuildStartingDeck(humanPlayer);

        if (!string.IsNullOrEmpty(BattleData.CurrentDeckKey))
        {
            Debug.Log("Loading deck by key: " + BattleData.CurrentDeckKey);
            LoadDeckByKey(aiPlayer, BattleData.CurrentDeckKey);
        }
        else
        {
            Debug.LogWarning("No deckKey set — using fallback starter deck.");
            DeckDatabase.BuildStarterDeck(aiPlayer);
        }

        PutStartingPermanentsOnBattlefield(humanPlayer);
        PutStartingPermanentsOnBattlefield(aiPlayer);

        ShuffleDeck(humanPlayer);
        ShuffleDeck(aiPlayer);

        DrawCards(humanPlayer, 7);
        DrawCards(aiPlayer, 7);
        UpdateUI();
    }

    void PutStartingPermanentsOnBattlefield(Player player)
    {
        if (player.StartingPermanents == null || player.StartingPermanents.Count == 0)
            return;

        foreach (var card in player.StartingPermanents)
        {
            SummonToken(card, player);
        }

        player.StartingPermanents.Clear();
    }

    void Update()
    {
        // Debug helpers for testing win and draw functions. Commented out for release build.
        // if (Input.GetKeyDown(KeyCode.W))
        // {
        //     if (!string.IsNullOrEmpty(BattleData.CurrentZoneId))
        //     {
        //         Debug.Log("[DEV] Instant win triggered for zone ID: " + BattleData.CurrentZoneId);
        //         gameOver = true;
        //         FindObjectOfType<WinScreenUI>().ShowWinScreen();
        //     }
        //     else
        //     {
        //         Debug.LogWarning("[DEV] No zone ID found — can't win");
        //     }
        // }

        // if (Input.GetKeyDown(KeyCode.D))
        // {
        //     GameManager.Instance.DrawCard(GameManager.Instance.humanPlayer);
        //     Debug.Log("D key pressed — drew a card.");
        // }
    }

    public void ShuffleDeck(Player player)
    {
        for (int i = 0; i < player.Deck.Count; i++)
        {
            Card temp = player.Deck[i];
            int randomIndex = Random.Range(i, player.Deck.Count);
            player.Deck[i] = player.Deck[randomIndex];
            player.Deck[randomIndex] = temp;
        }
    }

    public void DrawCards(Player player, int amount)
    {
        if (amount <= 0)
            return;

        for (int i = 0; i < amount; i++)
        {
            // only play the draw sound on the first card if this is the human player
            bool playSfx = (i == 0);
            DrawCard(player, playSfx);
        }
    }
    public void DrawCard(Player player, bool playSfx = true)
    {
        if (player.Deck.Count == 0)
        {
            if (player == aiPlayer)
            {
                Debug.Log("AI tried to draw from an empty deck — player wins by mill.");
                CardData reward = PlayerCollection.AddRandomCard();
                gameOver = true;
                FindObjectOfType<WinScreenUI>().ShowWinScreen(reward);
            }
            else if (player == humanPlayer)
            {
                Debug.Log("Player tried to draw from an empty deck — player loses by mill.");
                gameOver = true;
                FindObjectOfType<WinScreenUI>().ShowLoseScreen();
            }
            return;
        }

        Card card = player.Deck[0];
        player.Deck.RemoveAt(0);
        player.Hand.Add(card);

        if (player == humanPlayer)
        {
            GameObject obj = Instantiate(cardPrefab, playerHandArea);
            CardVisual visual = obj.GetComponent<CardVisual>();

            CardData sourceData = CardDatabase.GetCardData(card.cardName);
            visual.Setup(card, this, sourceData);

            activeCardVisuals.Add(visual);
        }

        NotifyCardDrawn(player, 1);
        NotifyOpponentDraw(player);
        if (player == humanPlayer && playSfx)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.drawCard);
        }
    }

    public void PlayCard(Player player, CardVisual visual)
    {
        if (IsStackActive())
        {
            Debug.Log("A spell is already on the stack. Please wait.");
            return;
        }

        Card card = visual.linkedCard;

        if (IsOnlyCastCreatureSpellsActive() && !(card is CreatureCard) && !(card is LandCard))
        {
            Debug.Log("Anti-Magic Grid prevents casting non-creature spells.");
            return;
        }

        if (card is LandCard)
        {
            if (player.hasPlayedLandThisTurn)
            {
                Debug.Log("You already played a land this turn!");
                return;
            }

            player.Battlefield.Add(card);
            player.Hand.Remove(card);
            player.hasPlayedLandThisTurn = true;

            if (card.entersTapped || GameManager.Instance.IsAllPermanentsEnterTappedActive())
            {
                card.isTapped = true;
                Debug.Log($"{card.cardName} enters tapped (static effect or base).");
            }

            card.OnEnterPlay(player);
            NotifyLandEntered(card, player);

            visual.transform.SetParent(player == humanPlayer ? playerLandArea : aiLandArea, false);
            visual.isInBattlefield = true;
            visual.UpdateVisual();
            SoundManager.Instance.PlaySound(SoundManager.Instance.cardPlay);

            AwardFavouriteCardCoins(card, player);

        }

        else if (card is CreatureCard creature)
        {
            var cost = GetManaCostBreakdown(card.manaCost, card.color);
            int tax = GetOpponentSpellTax(player);
            if (tax > 0)
            {
                if (!cost.ContainsKey("Colorless"))
                    cost["Colorless"] = 0;
                cost["Colorless"] += tax;
            }
            int reduction = GetCreatureCostReduction(player);
            CardData data = CardDatabase.GetCardData(card.cardName);
            if (data != null && data.subtypes.Contains("Beast"))
                reduction += GetBeastCreatureCostReduction(player);
            if (reduction > 0 && cost.ContainsKey("Colorless"))
                cost["Colorless"] = Mathf.Max(0, cost["Colorless"] - reduction);
            if (player.ColoredMana.CanPay(cost))
            {
                isStackBusy = true; // block other actions while creature is on stack
                player.ColoredMana.Pay(cost);
                if (card.hasXCost)
                {
                    card.xValue = player.ColoredMana.Total();
                    if (card.xValue > 0)
                        player.ColoredMana.SpendGeneric(card.xValue);
                }
                card.owner = player;
                if (player == humanPlayer) UpdateUI();
                player.Hand.Remove(card);

                // Move visual to the stack zone
                visual.transform.SetParent(stackZone, false);
                visual.isInStack = true;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                SoundManager.Instance.PlaySound(SoundManager.Instance.cardPlay);

                StartCoroutine(ResolveCreatureAfterDelay(creature, visual, player));
            }
            else
            {
                Debug.Log("Not enough colored mana to cast this creature.");
            }
        }

        else if (card is SorceryCard sorcery)
        {
            if (sorcery.requiresTarget)
            {
                Debug.Log("This sorcery requires a target — entering targeting mode.");
                BeginTargetSelection(sorcery, player, visual);
                return;
            }

            var cost = GetManaCostBreakdown(sorcery.manaCost, sorcery.color);
            int tax = GetOpponentSpellTax(player);
            if (tax > 0)
            {
                if (!cost.ContainsKey("Colorless"))
                    cost["Colorless"] = 0;
                cost["Colorless"] += tax;
            }
            if (player.ColoredMana.CanPay(cost))
            {
                isStackBusy = true; // BLOCK OTHER ACTIONS WHILE SORCERY IS ON STACK
                player.ColoredMana.Pay(cost);
                if (card.hasXCost)
                {
                    card.xValue = player.ColoredMana.Total();
                    if (card.xValue > 0)
                        player.ColoredMana.SpendGeneric(card.xValue);
                }
                card.owner = player;
                player.Hand.Remove(card);
                UpdateUI();

                // Move visual to the stack zone
                visual.transform.SetParent(stackZone, false);
                visual.isInStack = true;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                SoundManager.Instance.PlaySound(SoundManager.Instance.cardPlay);
                StartCoroutine(ResolveSorceryAfterDelay(sorcery, visual, player));
            }
            else
            {
                Debug.Log("Not enough colored mana to cast this sorcery.");
            }
        }
        else if (card is ArtifactCard artifact)
        {
            var cost = GetManaCostBreakdown(artifact.manaCost, artifact.color);
            int tax = GetOpponentSpellTax(player);
            if (tax > 0)
            {
                if (!cost.ContainsKey("Colorless"))
                    cost["Colorless"] = 0;
                cost["Colorless"] += tax;
            }
            CardData artData = CardDatabase.GetCardData(card.cardName);
            int reduction = (artData != null && artData.subtypes.Contains("Potion"))
                ? GetPotionCostReduction(player) : 0;
            if (reduction > 0 && cost.ContainsKey("Colorless"))
                cost["Colorless"] = Mathf.Max(0, cost["Colorless"] - reduction);
            if (player.ColoredMana.CanPay(cost))
            {
                isStackBusy = true;
                player.ColoredMana.Pay(cost);
                if (card.hasXCost)
                {
                    card.xValue = player.ColoredMana.Total();
                    if (card.xValue > 0)
                        player.ColoredMana.SpendGeneric(card.xValue);
                }
                card.owner = player;
                if (player == humanPlayer) UpdateUI();

                player.Hand.Remove(card);

                // Move visual to stack
                visual.transform.SetParent(stackZone, false);
                visual.isInStack = true;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                SoundManager.Instance.PlaySound(SoundManager.Instance.cardPlay);

                StartCoroutine(ResolveArtifactAfterDelay(artifact, visual, player));
            }
            else
            {
                Debug.Log("Not enough colored mana to play this artifact.");
            }
        }
        else if (card is AuraCard aura)
        {
            Debug.Log("Aura requires target — entering targeting mode.");
            BeginAuraTargetSelection(aura, player, visual);
            return;
        }
        else if (card is EnchantmentCard enchantment)
        {
            var cost = GetManaCostBreakdown(enchantment.manaCost, enchantment.color);
            int tax = GetOpponentSpellTax(player);
            if (tax > 0)
            {
                if (!cost.ContainsKey("Colorless"))
                    cost["Colorless"] = 0;
                cost["Colorless"] += tax;
            }
            if (player.ColoredMana.CanPay(cost))
            {
                isStackBusy = true;
                player.ColoredMana.Pay(cost);
                if (card.hasXCost)
                {
                    card.xValue = player.ColoredMana.Total();
                    if (card.xValue > 0)
                        player.ColoredMana.SpendGeneric(card.xValue);
                }
                card.owner = player;
                if (player == humanPlayer) UpdateUI();

                player.Hand.Remove(card);

                // Move visual to stack
                visual.transform.SetParent(stackZone, false);
                visual.isInStack = true;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                SoundManager.Instance.PlaySound(SoundManager.Instance.cardPlay);

                StartCoroutine(ResolveEnchantmentAfterDelay(enchantment, visual, player));
            }
            else
            {
                Debug.Log("Not enough colored mana to play this enchantment.");
            }
        }
        else
        {
            Debug.LogWarning("Unhandled card type played: " + card.cardName);
        }
    }

    public void TapLandForMana(LandCard land, Player player)
    {
        if (land.isTapped)
            return;

        land.isTapped = true;

        var colors = CardDatabase.GetCardData(land.cardName).color;
        string color = (colors != null && colors.Count > 0) ? colors[0] : "Colorless";

        switch (color)
        {
            case "White": player.ColoredMana.White++; break;
            case "Blue": player.ColoredMana.Blue++; break;
            case "Black": player.ColoredMana.Black++; break;
            case "Red": player.ColoredMana.Red++; break;
            case "Green": player.ColoredMana.Green++; break;
            default:
                Debug.LogWarning($"Unknown land color for mana: {color}");
                break;
        }

        if (player == humanPlayer)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.tap_for_mana);
            ShowManaVFX(land);
            UpdateUI();
        }
    }

    private void ShowManaVFX(LandCard land)
    {
        CardVisual visual = FindCardVisual(land);
        if (visual == null)
        {
            Debug.LogWarning("No visual found for land card " + land.cardName);
            return;
        }

        Vector3 spawnPos = visual.transform.position;
        spawnPos.z = 0f;

        Sprite iconSprite = GetManaIconForCardName(land.cardName);

        GameObject vfx = Instantiate(manaVFXPrefab, spawnPos, Quaternion.identity);
        vfx.GetComponentInChildren<SpriteRenderer>().sprite = iconSprite;

        Debug.Log("Spawning mana VFX at: " + spawnPos);
    }

    private Sprite GetManaIconForCardName(string cardName)
    {
        CardData data = CardDatabase.GetCardData(cardName);
        if (data == null)
        {
            Debug.LogWarning("No card data found for: " + cardName);
            return null;
        }

        string primaryColor = (data.color != null && data.color.Count > 0) ? data.color[0] : "None";

        switch (primaryColor)
        {
            case "Blue": return blueIcon;
            case "White": return whiteIcon;
            case "Black": return blackIcon;
            case "Red": return redIcon;
            case "Green": return greenIcon;
            default:
                Debug.LogWarning("Unknown color: " + data.color);
                return null;
        }
    }

    public void SendToGraveyard(Card card, Player owner, bool fromStack = false)
    {
        if (processedDeaths.Contains(card))
            return;

        processedDeaths.Add(card);

        bool diedFromBattlefield = owner.Battlefield.Contains(card);
        bool discardedFromHand = owner.Hand.Contains(card);
        Player graveyardOwner = card.owner ?? owner;

        if (fromStack)
        {
            Debug.Log($"{card.cardName} is going to the graveyard from the stack — skipping VFX.");

            CardVisual stackVisual = FindCardVisual(card);
            if (stackVisual != null)
            {
                activeCardVisuals.Remove(stackVisual);
                Destroy(stackVisual.gameObject);
            }

            if (!card.isToken)
            {
                GameObject visualGO = Instantiate(cardPrefab,
                    graveyardOwner == humanPlayer ? playerGraveyardArea : aiGraveyardArea);
                CardVisual stackGraveyardVisual = visualGO.GetComponent<CardVisual>();
                stackGraveyardVisual.Setup(card, this);
                stackGraveyardVisual.transform.localPosition = Vector3.zero;
                stackGraveyardVisual.UpdateGraveyardVisual();
                // Ensure logo and count remain on top
                // Last card in should appear on top, so send to end of hierarchy
                stackGraveyardVisual.transform.SetAsLastSibling();
                EnsureGraveyardCounterOnTop(graveyardOwner);

                activeCardVisuals.Add(stackGraveyardVisual);
            }

            graveyardOwner.Graveyard.Add(card);
            UpdateUI();
            return;
        }


        owner.Battlefield.Remove(card);
        owner.Hand.Remove(card);

        if (diedFromBattlefield && card is CreatureCard deadCreature)
        {
            currentAttackers.Remove(deadCreature);
            selectedAttackers.Remove(deadCreature);
            if (selectedBlockerForBlocking == deadCreature)
                selectedBlockerForBlocking = null;

            foreach (var creature in humanPlayer.Battlefield.Concat(aiPlayer.Battlefield).OfType<CreatureCard>())
            {
                creature.blockedByThisBlocker.Remove(deadCreature);
                if (creature.blockingThisAttacker == deadCreature)
                    creature.blockingThisAttacker = null;

                var vis = FindCardVisual(creature);
                if (vis != null)
                    vis.UpdateVisual();
            }
        }

        if (discardedFromHand)
        {
            NotifyOpponentDiscard(owner);
            NotifyPlayerDiscard(owner);
        }

        Debug.Log($"{card.cardName} is being sent to the graveyard.");

        if (card is CreatureCard && (diedFromBattlefield || discardedFromHand))
        {
            NotifyCreatureDiesOrDiscarded(card, owner);
        }
        if (card is CreatureCard && diedFromBattlefield)
        {
            NotifyCreatureDies(card, owner);
        }

        if (diedFromBattlefield)
        {
            card.OnLeavePlay(owner);
            if (card is LandCard)
                NotifyLandLeft(card, owner);

            if (card is CreatureCard leftCreature)
            {
                foreach (var player in new[] { humanPlayer, aiPlayer })
                {
                    var attached = player.Battlefield
                        .OfType<AuraCard>()
                        .Where(a => a.attachedTo == leftCreature)
                        .ToList();

                    foreach (var aura in attached)
                        SendToGraveyard(aura, player);

                    var equips = player.Battlefield
                        .OfType<EquipmentCard>()
                        .Where(e => e.equippedTo == leftCreature)
                        .ToList();

                    foreach (var eq in equips)
                    {
                        eq.Unequip();
                        FindCardVisual(eq)?.UpdateVisual();
                    }
                }
            }
        }

        card.isTapped = false;

        CardVisual visual = FindCardVisual(card); // <-- Moved up
        if (visual != null)
            visual.EnableTargetingHighlight(false); // ensure highlight removed
        if (visual != null && visual.tapIcon != null)
            visual.tapIcon.SetActive(false);

        if (discardedFromHand && visual != null)
        {
            StartCoroutine(ShowHandDiscardVFX(card, owner, visual));
            return;
        }

        if (card is CreatureCard thisDeadCreature)
        {
            thisDeadCreature.hasSummoningSickness = false;
            thisDeadCreature.toughness = thisDeadCreature.baseToughness;

            if (visual != null)
            {
                visual.sicknessText.text = "";
            }

            if (card.isToken && diedFromBattlefield)
            {
                if (visual != null)
                {
                    StartCoroutine(ShowDeathVFXAndDelayLayout(card, owner, visual));
                }
                return;
            }

            if (diedFromBattlefield && visual != null)
            {
                StartCoroutine(ShowDeathVFXAndDelayLayout(card, owner, visual));
                return;
            }
        }

        if (card is ArtifactCard && diedFromBattlefield && visual != null)
        {
            StartCoroutine(ShowDeathVFXAndDelayLayout(card, owner, visual, artifactDeathPrefab));
            return;
        }

        // Fallback: create graveyard visual normally
        CardVisual graveyardVisual = FindCardVisual(card);
        if (graveyardVisual == null)
        {
            GameObject visualGO = Instantiate(cardPrefab,
                graveyardOwner == humanPlayer ? playerGraveyardArea : aiGraveyardArea);
            graveyardVisual = visualGO.GetComponent<CardVisual>();
            graveyardVisual.Setup(card, this);
            activeCardVisuals.Add(graveyardVisual);
        }

        graveyardVisual.transform.SetParent(graveyardOwner == humanPlayer ? playerGraveyardArea : aiGraveyardArea);
        graveyardVisual.transform.localPosition = Vector3.zero;
        graveyardVisual.UpdateGraveyardVisual();
        // Ensure graveyard UI elements stay above the cards
        // Newest card should appear on top
        graveyardVisual.transform.SetAsLastSibling();
        EnsureGraveyardCounterOnTop(graveyardOwner);

        graveyardOwner.Graveyard.Add(card);
        UpdateUI();
    }

    public (int playerDamage, int aiDamage) ResolveCombat()
    {
        int playerDamage = 0;
        int aiDamage = 0;

        foreach (var attacker in currentAttackers)
        {
            // Clamp negative power to zero when dealing damage and handle damage prevention
            int attackerDamage = attacker.keywordAbilities.Contains(KeywordAbility.CantDealCombatDamage)
                ? 0
                : Mathf.Max(attacker.power, 0);
            var blockers = attacker.blockedByThisBlocker;

            if (blockers != null && blockers.Count > 0)
            {
                int remainingDamage = attackerDamage;
                int totalDamageFromBlockers = 0;

                bool attackerHasTrample = attacker.keywordAbilities.Contains(KeywordAbility.Trample);
                for (int i = 0; i < blockers.Count; i++)
                {
                    var blocker = blockers[i];
                    bool attackerProtected = blocker.color.Any(c => attacker.keywordAbilities.Contains(ProtectionUtils.GetProtectionKeyword(c)));
                    bool blockerProtected = attacker.color.Any(c => blocker.keywordAbilities.Contains(ProtectionUtils.GetProtectionKeyword(c)));

                    int damageToBlocker = 0;
                    if (!blockerProtected)
                    {
                        bool isLastBlocker = i == blockers.Count - 1;
                        if (!attackerHasTrample && isLastBlocker)
                            damageToBlocker = remainingDamage;
                        else
                            damageToBlocker = Mathf.Min(remainingDamage, blocker.toughness);

                        blocker.TakeDamage(damageToBlocker);
                        if (attacker.keywordAbilities.Contains(KeywordAbility.Deathtouch) && damageToBlocker > 0)
                            blocker.Kill();
                        remainingDamage -= damageToBlocker;
                    }

                    int damageFromBlocker = (attackerProtected || blocker.keywordAbilities.Contains(KeywordAbility.CantDealCombatDamage))
                        ? 0
                        : Mathf.Max(blocker.power, 0);
                    if (!attackerProtected)
                    {
                        totalDamageFromBlockers += damageFromBlocker;
                        if (blocker.keywordAbilities.Contains(KeywordAbility.Deathtouch) && damageFromBlocker > 0)
                            attacker.Kill();
                    }

                    if (damageToBlocker > 0 || damageFromBlocker > 0)
                        SoundManager.Instance.PlaySound(SoundManager.Instance.impact);

                    Debug.Log($"{attacker.cardName} is blocked by {blocker.cardName}.");

                    if (attacker.keywordAbilities.Contains(KeywordAbility.Lifelink) && damageToBlocker > 0)
                    {
                        Player owner = GetOwnerOfCard(attacker);
                        GameManager.Instance.TryGainLife(owner, damageToBlocker);
                        Debug.Log($"{attacker.cardName} lifelinks {damageToBlocker} life to {owner}.");
                    }

                    if (blocker.keywordAbilities.Contains(KeywordAbility.Lifelink) && damageFromBlocker > 0)
                    {
                        Player blockerOwner = GetOwnerOfCard(blocker);
                        GameManager.Instance.TryGainLife(blockerOwner, damageFromBlocker);
                        Debug.Log($"{blocker.cardName} lifelinks {damageFromBlocker} life to {blockerOwner}.");
                    }
                }

                attacker.TakeDamage(totalDamageFromBlockers);

                if (attacker.keywordAbilities.Contains(KeywordAbility.Trample) && remainingDamage > 0)
                {
                    if (humanPlayer.Battlefield.Contains(attacker))
                    {
                        aiPlayer.Life -= remainingDamage;
                        aiDamage += remainingDamage;
                        Debug.Log($"{attacker.cardName} tramples over for {remainingDamage} damage!");
                        NotifyCombatDamageToPlayer(attacker, aiPlayer);
                    }
                    else
                    {
                        humanPlayer.Life -= remainingDamage;
                        playerDamage += remainingDamage;
                        Debug.Log($"{attacker.cardName} tramples YOU for {remainingDamage} damage!");
                        NotifyCombatDamageToPlayer(attacker, humanPlayer);
                    }

                    if (attacker.keywordAbilities.Contains(KeywordAbility.Lifelink))
                    {
                        GameManager.Instance.TryGainLife(GetOwnerOfCard(attacker), remainingDamage);
                        Debug.Log($"{attacker.cardName} lifelinks {remainingDamage} trample damage.");
                    }
                }
            }
            else
            {
                // Attacker goes unblocked
                if (humanPlayer.Battlefield.Contains(attacker))
                {
                    aiPlayer.Life -= attackerDamage;
                    aiDamage += attackerDamage;
                    NotifyCombatDamageToPlayer(attacker, aiPlayer);

                    // Lifelink: gain life equal to damage dealt to AI
                    if (attacker.keywordAbilities.Contains(KeywordAbility.Lifelink))
                    {
                        Player owner = humanPlayer.Battlefield.Contains(attacker) ? humanPlayer : aiPlayer;
                        GameManager.Instance.TryGainLife(owner, attackerDamage);
                        Debug.Log($"{attacker.cardName} lifelinks {attackerDamage} life to {(owner == humanPlayer ? "Human" : "AI")}.");
                    }
                }
                else
                {
                    humanPlayer.Life -= attackerDamage;
                    playerDamage += attackerDamage;
                    NotifyCombatDamageToPlayer(attacker, humanPlayer);

                    // Lifelink: gain life equal to damage dealt to Human
                    if (attacker.keywordAbilities.Contains(KeywordAbility.Lifelink))
                    {
                        GameManager.Instance.TryGainLife(aiPlayer, attackerDamage);
                        Debug.Log($"{attacker.cardName} lifelinks {attackerDamage} life to AI.");
                    }
                }
            }
        }

        foreach (var creature in humanPlayer.Battlefield.Concat(aiPlayer.Battlefield).OfType<CreatureCard>())
        {
            if (creature.cardName == "Undead Army" &&
                (currentAttackers.Contains(creature) || creature.blockingThisAttacker != null))
            {
                creature.AddMinusOneCounter();
                var vis = FindCardVisual(creature);
                if (vis != null) vis.UpdateVisual();
            }
        }

        CheckDeaths(humanPlayer);
        CheckDeaths(aiPlayer);

        // Cleanup combat assignments
        foreach (var card in humanPlayer.Battlefield)
        {
            if (card is CreatureCard c)
            {
                c.blockingThisAttacker = null;
                c.blockedByThisBlocker.Clear();
            }
        }
        foreach (var card in aiPlayer.Battlefield)
        {
            if (card is CreatureCard c)
            {
                c.blockingThisAttacker = null;
                c.blockedByThisBlocker.Clear();
            }
        }

        currentAttackers.Clear();
        selectedBlockerForBlocking = null;
        UpdateUI();
        CheckForGameEnd();
        return (playerDamage, aiDamage);
    }

    public void CheckDeaths(Player player)
    {
        List<Card> toGrave = new List<Card>();
        foreach (var card in player.Battlefield)
        {
            if (card is CreatureCard c && c.toughness <= 0)
            {
                if (!processedDeaths.Contains(c))
                    toGrave.Add(c);
            }
        }

        foreach (var card in toGrave)
        {
            SendToGraveyard(card, player);
        }
    }

    public void ResetDeathTracking()
    {
        processedDeaths.Clear();
    }

    public void ResetPermanents(Player player)
    {
        foreach (var card in player.Battlefield)
        {
            bool cantUntap = false;
            if (card is CreatureCard checkCreature)
                cantUntap = checkCreature.keywordAbilities.Contains(KeywordAbility.CantUntap);
            else if (card.keywordAbilities != null)
                cantUntap = card.keywordAbilities.Contains(KeywordAbility.CantUntap);

            if (!cantUntap)
                card.isTapped = false;

            if (card is CreatureCard creature)
            {
                creature.hasSummoningSickness = false;
                creature.RecalculateStats();
                creature.blockingThisAttacker = null;
                creature.blockedByThisBlocker.Clear();
            }

            var visual = FindCardVisual(card);
            if (visual != null)
            {
                visual.UpdateVisual(); // Just call once, it's enough
            }
        }

        player.hasPlayedLandThisTurn = false; // Also reset land play

        foreach (var visual in activeCardVisuals)
        {
            visual.UpdateVisual();
        }

    }

    public void ResetDamage(Player player)
    {
        foreach (var card in player.Battlefield)
        {
            if (card is CreatureCard creature)
            {
                creature.ResetDamage();
            }
        }
    }

    public CardVisual FindCardVisual(Card card)
    {
        return activeCardVisuals.Find(cv => cv.linkedCard == card);
    }

    public IEnumerator ResolveSorceryAfterDelay(SorceryCard sorcery, CardVisual visual, Player caster)
    {
        yield return new WaitForSeconds(2f);

        // PREVENT executing if required target is missing
        if (sorcery.requiresTarget && sorcery.chosenTarget == null && sorcery.chosenPlayerTarget == null)
        {
            Debug.LogWarning($"[ResolveSorceryAfterDelay] {sorcery.cardName} requires a target, but none was set. Aborting cast.");

            // Destroy visual
            if (visual != null)
            {
                GameManager.Instance.activeCardVisuals.Remove(visual);
                GameObject.Destroy(visual.gameObject);
            }

            isStackBusy = false;
            yield break;
        }

        if (sorcery.chosenTarget != null)
        {
            sorcery.ResolveEffect(caster, sorcery.chosenTarget);
        }
        else if (sorcery.chosenPlayerTarget != null)
        {
            sorcery.ResolveEffectOnPlayer(caster, sorcery.chosenPlayerTarget);
        }
        else
        {
            sorcery.ResolveEffect(caster);
        }

        SendToGraveyard(sorcery, caster, fromStack: true);
        AwardFavouriteCardCoins(sorcery, caster);

        if (caster == aiPlayer && visual != null)
        {
            activeCardVisuals.Remove(visual);
            Destroy(visual.gameObject);
        }

        UpdateUI();
        isStackBusy = false;
        CheckForGameEnd();

        if (caster == aiPlayer && TurnSystem.Instance.waitingToResumeAI && pendingStackEffects == 0)
        {
            Debug.Log("Resuming AI phase after stack.");
            TurnSystem.Instance.waitingToResumeAI = false;
            TurnSystem.Instance.RunSpecificPhase(TurnSystem.Instance.lastPhaseBeforeStack);
        }
    }

    public IEnumerator ResolveCreatureAfterDelay(CreatureCard creature, CardVisual visual, Player caster)
    {
        yield return new WaitForSeconds(2f);

        caster.Battlefield.Add(creature);

        if (creature.keywordAbilities.Contains(KeywordAbility.Haste))
            creature.hasSummoningSickness = false;
        else
            creature.hasSummoningSickness = true;

        if (creature.entersTapped || IsAllPermanentsEnterTappedActive())
        {
            creature.isTapped = true;
            Debug.Log($"{creature.cardName} enters tapped (due to static effect).");
        }

        Transform battlefield = caster == humanPlayer ? playerBattlefieldArea : aiBattlefieldArea;
        visual.transform.SetParent(battlefield, false);
        visual.isInStack = false;
        visual.isInBattlefield = true;
        if (!activeCardVisuals.Contains(visual))
            activeCardVisuals.Add(visual);
        visual.UpdateVisual();

        creature.OnEnterPlay(caster);

        if (caster == aiPlayer && creature.abilities != null)
        {
            foreach (var ability in creature.abilities)
            {
                if (ability.timing == TriggerTiming.OnEnter && ability.requiresTarget)
                {
                    Player opponent = GetOpponentOf(caster);
                    Card target = opponent.Battlefield
                        .Where(c =>
                            (ability.requiredTargetType == SorceryCard.TargetType.Creature && c is CreatureCard creatureT &&
                                !(ability.excludeArtifactCreatures && creatureT.color.Contains("Artifact"))) ||
                            (ability.requiredTargetType == SorceryCard.TargetType.Artifact && c is ArtifactCard) ||
                            (ability.requiredTargetType == SorceryCard.TargetType.Enchantment && c is EnchantmentCard) ||
                            (ability.requiredTargetType == SorceryCard.TargetType.Land && c is LandCard))
                        .OrderByDescending(c => CardDatabase.GetCardData(c.cardName)?.manaCost ?? 0)
                        .FirstOrDefault();

                    if (target != null)
                    {
                        QueueTriggeredAbility(ability, caster, creature, target);
                        Debug.Log($"[AI ETB] {creature.cardName} targets {target.cardName}");
                    }
                }
            }
        }

        NotifyCreatureEntered(creature, caster);
        if (creature.color.Contains("Artifact"))
            NotifyArtifactEntered(creature, caster);

        AwardFavouriteCardCoins(creature, caster);

        SoundManager.Instance.PlaySound(SoundManager.Instance.playCreature);

        UpdateUI();
        isStackBusy = false;
        CheckForGameEnd();

        if (caster == aiPlayer && TurnSystem.Instance.waitingToResumeAI && pendingStackEffects == 0)
        {
            Debug.Log("Resuming AI phase after stack.");
            TurnSystem.Instance.waitingToResumeAI = false;
            TurnSystem.Instance.RunSpecificPhase(TurnSystem.Instance.lastPhaseBeforeStack);
        }
    }

    public IEnumerator ResolveArtifactAfterDelay(ArtifactCard artifact, CardVisual visual, Player caster)
    {
        yield return new WaitForSeconds(2f);

        caster.Battlefield.Add(artifact);

        if (artifact.entersTapped || IsAllPermanentsEnterTappedActive())
        {
            artifact.isTapped = true;
            Debug.Log($"{artifact.cardName} enters tapped (due to static effect).");
        }

        Transform area = caster == humanPlayer ? playerArtifactArea : aiArtifactArea;
        visual.transform.SetParent(area, false);
        visual.isInStack = false;
        visual.isInBattlefield = true;
        if (!activeCardVisuals.Contains(visual))
            activeCardVisuals.Add(visual);
        visual.UpdateVisual();

        artifact.OnEnterPlay(caster);
        NotifyArtifactEntered(artifact, caster);

        AwardFavouriteCardCoins(artifact, caster);

        SoundManager.Instance.PlaySound(SoundManager.Instance.playArtifact);

        UpdateUI();
        isStackBusy = false;
        CheckForGameEnd();

        if (caster == aiPlayer && TurnSystem.Instance.waitingToResumeAI && pendingStackEffects == 0)
        {
            Debug.Log("Resuming AI phase after stack.");
            TurnSystem.Instance.waitingToResumeAI = false;
            TurnSystem.Instance.RunSpecificPhase(TurnSystem.Instance.lastPhaseBeforeStack);
        }
    }

    public IEnumerator ResolveEnchantmentAfterDelay(EnchantmentCard enchantment, CardVisual visual, Player caster)
    {
        yield return new WaitForSeconds(2f);

        caster.Battlefield.Add(enchantment);

        if (enchantment.entersTapped || IsAllPermanentsEnterTappedActive())
        {
            enchantment.isTapped = true;
            Debug.Log($"{enchantment.cardName} enters tapped (due to static effect).");
        }

        Transform area = caster == humanPlayer ? playerEnchantmentArea : aiEnchantmentArea;
        visual.transform.SetParent(area, false);
        visual.isInStack = false;
        visual.isInBattlefield = true;
        if (!activeCardVisuals.Contains(visual))
            activeCardVisuals.Add(visual);
        visual.UpdateVisual();

        enchantment.OnEnterPlay(caster);
        NotifyEnchantmentEntered(enchantment, caster);

        AwardFavouriteCardCoins(enchantment, caster);

        SoundManager.Instance.PlaySound(SoundManager.Instance.playArtifact);

        UpdateUI();
        isStackBusy = false;
        CheckForGameEnd();

        if (caster == aiPlayer && TurnSystem.Instance.waitingToResumeAI && pendingStackEffects == 0)
        {
            Debug.Log("Resuming AI phase after stack.");
            TurnSystem.Instance.waitingToResumeAI = false;
            TurnSystem.Instance.RunSpecificPhase(TurnSystem.Instance.lastPhaseBeforeStack);
        }
    }

    public IEnumerator ResolveAuraAfterDelay(AuraCard aura, CardVisual visual, Player caster)
    {
        yield return new WaitForSeconds(2f);

        caster.Battlefield.Add(aura);
        aura.OnEnterPlay(caster);
        NotifyEnchantmentEntered(aura, caster);

        AwardFavouriteCardCoins(aura, caster);

        if (!caster.Battlefield.Contains(aura))
        {
            // Aura may have destroyed itself via its effect
            UpdateUI();
            isStackBusy = false;
            if (caster == aiPlayer && TurnSystem.Instance.waitingToResumeAI && pendingStackEffects == 0)
            {
                TurnSystem.Instance.waitingToResumeAI = false;
                TurnSystem.Instance.RunSpecificPhase(TurnSystem.Instance.lastPhaseBeforeStack);
            }
            yield break;
        }

        if (aura.entersTapped || IsAllPermanentsEnterTappedActive())
        {
            aura.isTapped = true;
            Debug.Log($"{aura.cardName} enters tapped (due to static effect).");
        }

        Transform area = caster == humanPlayer ? playerEnchantmentArea : aiEnchantmentArea;
        if (visual != null)
        {
            visual.transform.SetParent(area, false);
            visual.isInStack = false;
            visual.isInBattlefield = true;
            if (!activeCardVisuals.Contains(visual))
                activeCardVisuals.Add(visual);
            visual.UpdateVisual();
        }

        SoundManager.Instance.PlaySound(SoundManager.Instance.playArtifact);

        UpdateUI();
        isStackBusy = false;
        CheckForGameEnd();

        if (caster == aiPlayer && TurnSystem.Instance.waitingToResumeAI && pendingStackEffects == 0)
        {
            Debug.Log("Resuming AI phase after stack.");
            TurnSystem.Instance.waitingToResumeAI = false;
            TurnSystem.Instance.RunSpecificPhase(TurnSystem.Instance.lastPhaseBeforeStack);
        }
    }

    public IEnumerator ResolveTriggeredAbilityOnStack(
        CardAbility ability,
        Player owner,
        Card source,
        Card target,
        Card deadCreature = null)
    {
        yield return new WaitUntil(() => !isStackBusy);
        isStackBusy = true;
        TurnSystem.Instance.lastPhaseBeforeStack = TurnSystem.Instance.currentPhase;

        GameObject stackObj = Instantiate(cardPrefab, stackZone);
        CardVisual stackVisual = stackObj.GetComponent<CardVisual>();
        stackVisual.Setup(source, this);
        stackVisual.isInStack = true;
        stackVisual.UpdateVisual();
        stackVisual.transform.localPosition = Vector3.zero;
        stackVisual.transform.localRotation = Quaternion.identity;
        stackVisual.transform.localScale = Vector3.one;

        GameObject triggerVFX = null;
        if (triggerVFXPrefab != null)
        {
            triggerVFX = Instantiate(triggerVFXPrefab, stackObj.transform);
            RectTransform rt = triggerVFX.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = Vector2.zero;
        }

        yield return new WaitForSeconds(2f);

        Card previousDead = lastDeadCreature;
        if (deadCreature != null)
            lastDeadCreature = deadCreature;

        Card previousSource = lastAbilitySource;
        lastAbilitySource = source;

        int oldLife = owner.Life;
        ability.effect?.Invoke(owner, target);
        int gained = owner.Life - oldLife;
        if (gained > 0)
        {
            VisualEffectManager.Instance.ShowFloatingHealForPlayer(gained, owner == humanPlayer);
        }

        if (deadCreature != null)
            lastDeadCreature = previousDead;

        lastAbilitySource = previousSource;

        CheckDeaths(humanPlayer);
        CheckDeaths(aiPlayer);
        UpdateUI();
        optionalTargetPlayer = null;

        if (triggerVFX != null)
            Destroy(triggerVFX);
        Destroy(stackObj);
        isStackBusy = false;
        pendingStackEffects = Mathf.Max(0, pendingStackEffects - 1);
        CheckForGameEnd();

        if (owner == aiPlayer && TurnSystem.Instance.waitingToResumeAI && pendingStackEffects == 0)
        {
            Debug.Log("Resuming AI phase after stack.");
            TurnSystem.Instance.waitingToResumeAI = false;
            TurnSystem.Instance.RunSpecificPhase(TurnSystem.Instance.lastPhaseBeforeStack);
        }
    }

    public void QueueArtifactActivatedAbility(ArtifactCard artifact, ActivatedAbility ability, Player controller, Card target = null)
    {
        pendingStackEffects++;
        StartCoroutine(ResolveArtifactActivatedAbilityOnStack(artifact, ability, controller, target));
    }

    public IEnumerator ResolveArtifactActivatedAbilityOnStack(ArtifactCard artifact, ActivatedAbility ability, Player controller, Card target = null)
    {
        yield return new WaitUntil(() => !isStackBusy);
        isStackBusy = true;
        TurnSystem.Instance.lastPhaseBeforeStack = TurnSystem.Instance.currentPhase;

        GameObject stackObj = Instantiate(cardPrefab, stackZone);
        CardVisual stackVisual = stackObj.GetComponent<CardVisual>();
        stackVisual.Setup(artifact, this);
        stackVisual.isInStack = true;
        stackVisual.UpdateVisual();
        stackVisual.transform.localPosition = Vector3.zero;
        stackVisual.transform.localRotation = Quaternion.identity;
        stackVisual.transform.localScale = Vector3.one;

        GameObject triggerVFX = null;
        if (triggerVFXPrefab != null)
        {
            triggerVFX = Instantiate(triggerVFXPrefab, stackObj.transform);
            RectTransform rt = triggerVFX.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = Vector2.zero;
        }

        yield return new WaitForSeconds(2f);

        try
        {
            switch (ability)
            {
                case ActivatedAbility.TapToGainLife:
                    TryGainLife(controller, 1);
                    break;
                case ActivatedAbility.TapToPlague:
                    humanPlayer.Life -= artifact.plagueAmount;
                    aiPlayer.Life -= artifact.plagueAmount;
                    VisualEffectManager.Instance.ShowFloatingDamageForPlayer(artifact.plagueAmount, true);
                    VisualEffectManager.Instance.ShowFloatingDamageForPlayer(artifact.plagueAmount, false);
                    SoundManager.Instance.PlaySound(SoundManager.Instance.plague);
                    ShowBloodSplatVFX(artifact);
                    break;
                case ActivatedAbility.SacrificeForLife:
                    TryGainLife(controller, artifact.lifeToGain);
                    SoundManager.Instance.PlaySound(SoundManager.Instance.drink);
                    SoundManager.Instance.PlaySound(SoundManager.Instance.gain_life);
                    break;
                case ActivatedAbility.SacrificeToDrawCards:
                    DrawCards(controller, artifact.cardsToDraw);
                    break;
                case ActivatedAbility.TapToPlayRandomPotion:
                    SearchLibraryForRandomPotionToBattlefield(controller);
                    break;
                case ActivatedAbility.DealDamageToCreature:
                    if (target is CreatureCard targetCreature)
                    {
                        targetCreature.TakeDamage(artifact.damageToCreature);
                        Card asCard = artifact;
                        if (asCard is CreatureCard srcCreature &&
                            srcCreature.keywordAbilities.Contains(KeywordAbility.Deathtouch) &&
                            artifact.damageToCreature > 0)
                        {
                            targetCreature.Kill();
                        }
                    }
                    break;
                case ActivatedAbility.BuffTargetCreature:
                    if (target is CreatureCard buffTarget)
                    {
                        buffTarget.AddTemporaryBuff(artifact.buffPower, artifact.buffToughness);
                        CardVisual tVis = FindCardVisual(buffTarget);
                        if (tVis != null)
                            tVis.UpdateVisual();
                    }
                    break;
            }

            CheckDeaths(humanPlayer);
            CheckDeaths(aiPlayer);
            UpdateUI();
        }
        finally
        {
            if (triggerVFX != null)
                Destroy(triggerVFX);
            Destroy(stackObj);
            isStackBusy = false;
            pendingStackEffects = Mathf.Max(0, pendingStackEffects - 1);
            CheckForGameEnd();

            if (controller == aiPlayer && TurnSystem.Instance.waitingToResumeAI && pendingStackEffects == 0)
            {
                Debug.Log("Resuming AI phase after stack.");
                TurnSystem.Instance.waitingToResumeAI = false;
                TurnSystem.Instance.RunSpecificPhase(TurnSystem.Instance.lastPhaseBeforeStack);
            }
        }
    }

    public void QueueCreatureActivatedAbility(CreatureCard creature, ActivatedAbility ability, Player controller, Card target = null)
    {
        pendingStackEffects++;
        StartCoroutine(ResolveCreatureActivatedAbilityOnStack(creature, ability, controller, target));
    }

    public IEnumerator ResolveCreatureActivatedAbilityOnStack(CreatureCard creature, ActivatedAbility ability, Player controller, Card target = null)
    {
        yield return new WaitUntil(() => !isStackBusy);
        isStackBusy = true;
        TurnSystem.Instance.lastPhaseBeforeStack = TurnSystem.Instance.currentPhase;

        GameObject stackObj = Instantiate(cardPrefab, stackZone);
        CardVisual stackVisual = stackObj.GetComponent<CardVisual>();
        stackVisual.Setup(creature, this);
        stackVisual.isInStack = true;
        stackVisual.UpdateVisual();
        stackVisual.transform.localPosition = Vector3.zero;
        stackVisual.transform.localRotation = Quaternion.identity;
        stackVisual.transform.localScale = Vector3.one;

        GameObject triggerVFX = null;
        if (triggerVFXPrefab != null)
        {
            triggerVFX = Instantiate(triggerVFXPrefab, stackObj.transform);
            RectTransform rt = triggerVFX.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = Vector2.zero;
        }

        yield return new WaitForSeconds(2f);

        switch (ability)
        {
            case ActivatedAbility.PayToGainAbility:
                PayToGainAbility(creature);
                break;
            case ActivatedAbility.PayToBuffSelf:
                PayToBuffSelf(creature);
                break;
            case ActivatedAbility.TapToLoseLife:
                Player opponent = GetOpponentOf(controller);
                opponent.Life -= creature.tapLifeLossAmount;
                VisualEffectManager.Instance.ShowFloatingDamageForPlayer(creature.tapLifeLossAmount, controller == humanPlayer);
                SoundManager.Instance.PlaySound(SoundManager.Instance.plague);
                ShowBloodSplatVFX(creature);
                break;
            case ActivatedAbility.TapToCreateToken:
                string tokenName = creature.tokenToCreate;
                Card token = CardFactory.Create(tokenName);
                if (token != null)
                {
                    if (tokenName == "Autonomous Miner")
                        SoundManager.Instance.PlaySound(SoundManager.Instance.miner);
                    SummonToken(token, controller);
                    Debug.Log($"{creature.cardName} created a {tokenName} token.");
                }
                else
                {
                    Debug.LogError($"Failed to create token: {tokenName}");
                }
                break;
            case ActivatedAbility.ReturnSelfFromGraveyard:
                ReturnCreatureFromGraveyardToBattlefield(controller, creature);
                break;
            case ActivatedAbility.ReturnSelfFromGraveyardToHand:
                ReturnCreatureFromGraveyardToHand(controller, creature);
                break;
        }

        CheckDeaths(humanPlayer);
        CheckDeaths(aiPlayer);
        UpdateUI();

        if (triggerVFX != null)
            Destroy(triggerVFX);
        Destroy(stackObj);
        isStackBusy = false;
        pendingStackEffects = Mathf.Max(0, pendingStackEffects - 1);
        CheckForGameEnd();

        if (controller == aiPlayer && TurnSystem.Instance.waitingToResumeAI && pendingStackEffects == 0)
        {
            Debug.Log("Resuming AI phase after stack.");
            TurnSystem.Instance.waitingToResumeAI = false;
            TurnSystem.Instance.RunSpecificPhase(TurnSystem.Instance.lastPhaseBeforeStack);
        }
    }

    public void SummonToken(Card tokenCard, Player owner)
    {
        if (tokenCard == null)
        {
            Debug.LogError("Tried to summon a null token.");
            return;
        }

        tokenCard.owner = owner;

        if (tokenCard is CreatureCard creature)
        {
            creature.hasSummoningSickness = true;

            if (creature.entersTapped || GameManager.Instance.IsAllPermanentsEnterTappedActive())
            {
                creature.isTapped = true;
                Debug.Log($"{creature.cardName} enters tapped (due to static effect).");
            }
        }

        owner.Battlefield.Add(tokenCard);

        // Choose the correct battlefield area based on card type
        Transform area;
        if (tokenCard is LandCard)
            area = owner == humanPlayer ? playerLandArea : aiLandArea;
        else if (tokenCard is ArtifactCard)
            area = owner == humanPlayer ? playerArtifactArea : aiArtifactArea;
        else if (tokenCard is EnchantmentCard)
            area = owner == humanPlayer ? playerEnchantmentArea : aiEnchantmentArea;
        else
            area = owner == humanPlayer ? playerBattlefieldArea : aiBattlefieldArea;

        // Create visual and link it
        GameObject visualGO = Instantiate(cardPrefab, area);
        CardVisual visual = visualGO.GetComponent<CardVisual>();

        visual.Setup(tokenCard, this);
        visual.isInBattlefield = true;
        activeCardVisuals.Add(visual);
        visual.UpdateVisual();

        tokenCard.OnEnterPlay(owner);  // Run ETB triggers (last)
        if (tokenCard is CreatureCard)
            NotifyCreatureEntered(tokenCard, owner);
        if (tokenCard is LandCard)
            NotifyLandEntered(tokenCard, owner);
        if ((tokenCard is ArtifactCard) ||
            (tokenCard is CreatureCard cc && cc.color.Contains("Artifact")))
        {
            NotifyArtifactEntered(tokenCard, owner);
        }
        if (tokenCard is EnchantmentCard)
        {
            NotifyEnchantmentEntered(tokenCard, owner);
        }
    }
    public Player GetOpponentOf(Player player)
    {
        return player == humanPlayer ? aiPlayer : humanPlayer;
    }

    public void ReturnRandomLandFromGraveyard(Player player)
    {
        var lands = player.Graveyard.OfType<LandCard>().ToList();
        if (lands.Count == 0)
            return;

        Card chosen = lands[Random.Range(0, lands.Count)];
        player.Graveyard.Remove(chosen);
        player.Hand.Add(chosen);

        if (player == humanPlayer)
        {
            GameObject obj = Instantiate(cardPrefab, playerHandArea);
            CardVisual visual = obj.GetComponent<CardVisual>();
            CardData data = CardDatabase.GetCardData(chosen.cardName);
            visual.Setup(chosen, this, data);
            activeCardVisuals.Add(visual);
        }
        else if (enemyHandText != null)
        {
            enemyHandText.text = "Hand: " + player.Hand.Count;
        }

        RefreshGraveyardVisuals(player);
        UpdateUI();
    }

    public void ReturnRandomCreatureFromGraveyard(Player player)
    {
        var creatures = player.Graveyard.OfType<CreatureCard>().ToList();
        if (creatures.Count == 0)
            return;

        Card chosen = creatures[Random.Range(0, creatures.Count)];
        player.Graveyard.Remove(chosen);
        player.Hand.Add(chosen);

        if (player == humanPlayer)
        {
            GameObject obj = Instantiate(cardPrefab, playerHandArea);
            CardVisual visual = obj.GetComponent<CardVisual>();
            CardData data = CardDatabase.GetCardData(chosen.cardName);
            visual.Setup(chosen, this, data);
            activeCardVisuals.Add(visual);
        }
        else if (enemyHandText != null)
        {
            enemyHandText.text = "Hand: " + player.Hand.Count;
        }

        RefreshGraveyardVisuals(player);
        UpdateUI();
    }

    public void ReturnRandomNonCreatureArtifactFromGraveyard(Player player)
    {
        var artifacts = player.Graveyard
            .Where(card => card is ArtifactCard && !(card is CreatureCard))
            .ToList();
        if (artifacts.Count == 0)
            return;

        Card chosen = artifacts[Random.Range(0, artifacts.Count)];
        player.Graveyard.Remove(chosen);
        player.Hand.Add(chosen);

        if (player == humanPlayer)
        {
            GameObject obj = Instantiate(cardPrefab, playerHandArea);
            CardVisual visual = obj.GetComponent<CardVisual>();
            CardData data = CardDatabase.GetCardData(chosen.cardName);
            visual.Setup(chosen, this, data);
            activeCardVisuals.Add(visual);
        }
        else if (enemyHandText != null)
        {
            enemyHandText.text = "Hand: " + player.Hand.Count;
        }

        RefreshGraveyardVisuals(player);
        UpdateUI();
    }

    public void ReturnRandomPotionFromGraveyardToBattlefield(Player player)
    {
        var potions = player.Graveyard
            .Where(card => card.subtypes.Contains("Potion"))
            .ToList();
        if (potions.Count == 0)
            return;

        Card chosen = potions[Random.Range(0, potions.Count)];
        player.Graveyard.Remove(chosen);
        player.Battlefield.Add(chosen);

        if (chosen is CreatureCard creature)
        {
            creature.hasSummoningSickness = true;
            if (creature.entersTapped || IsAllPermanentsEnterTappedActive())
            {
                creature.isTapped = true;
                Debug.Log($"{creature.cardName} enters tapped (due to static effect).");
            }
        }
        else if (chosen is ArtifactCard artifact)
        {
            if (artifact.entersTapped || IsAllPermanentsEnterTappedActive())
            {
                artifact.isTapped = true;
                Debug.Log($"{artifact.cardName} enters tapped (due to static effect).");
            }
        }

        GameObject obj = Instantiate(cardPrefab,
            player == humanPlayer ?
                (chosen is ArtifactCard ? playerArtifactArea : playerBattlefieldArea) :
                (chosen is ArtifactCard ? aiArtifactArea : aiBattlefieldArea));
        CardVisual visual = obj.GetComponent<CardVisual>();
        CardData data = CardDatabase.GetCardData(chosen.cardName);
        visual.Setup(chosen, this, data);
        visual.isInBattlefield = true;
        activeCardVisuals.Add(visual);
        visual.UpdateVisual();

        chosen.OnEnterPlay(player);
        if (chosen is CreatureCard)
            NotifyCreatureEntered(chosen, player);
        if (chosen is LandCard)
            NotifyLandEntered(chosen, player);
        if ((chosen is ArtifactCard) || (chosen is CreatureCard cc && cc.color.Contains("Artifact")))
            NotifyArtifactEntered(chosen, player);
        if (chosen is EnchantmentCard)
            NotifyEnchantmentEntered(chosen, player);

        RefreshGraveyardVisuals(player);
        UpdateUI();
    }

    public void SearchLibraryForRandomPotion(Player player)
    {
        var potions = player.Deck
            .Where(card => card.subtypes.Contains("Potion"))
            .ToList();
        if (potions.Count == 0)
        {
            ShuffleDeck(player);
            return;
        }

        Card chosen = potions[Random.Range(0, potions.Count)];
        player.Deck.Remove(chosen);
        player.Hand.Add(chosen);

        if (player == humanPlayer)
        {
            GameObject obj = Instantiate(cardPrefab, playerHandArea);
            CardVisual visual = obj.GetComponent<CardVisual>();
            CardData data = CardDatabase.GetCardData(chosen.cardName);
            visual.Setup(chosen, this, data);
            activeCardVisuals.Add(visual);
        }
        else if (enemyHandText != null)
        {
            enemyHandText.text = "Hand: " + player.Hand.Count;
        }

        ShuffleDeck(player);
        UpdateUI();
    }

    public void SearchLibraryForRandomPotionToBattlefield(Player player)
    {
        var potions = player.Deck
            .Where(card => card.subtypes.Contains("Potion"))
            .ToList();
        if (potions.Count == 0)
        {
            ShuffleDeck(player);
            UpdateUI();
            return;
        }

        Card chosen = potions[Random.Range(0, potions.Count)];
        player.Deck.Remove(chosen);
        player.Battlefield.Add(chosen);

        if (chosen is CreatureCard creature)
        {
            creature.hasSummoningSickness = true;
            if (creature.entersTapped || IsAllPermanentsEnterTappedActive())
            {
                creature.isTapped = true;
                Debug.Log($"{creature.cardName} enters tapped (due to static effect).");
            }
        }
        else if (chosen is ArtifactCard artifact)
        {
            if (artifact.entersTapped || IsAllPermanentsEnterTappedActive())
            {
                artifact.isTapped = true;
                Debug.Log($"{artifact.cardName} enters tapped (due to static effect).");
            }
        }

        GameObject obj = Instantiate(cardPrefab,
            player == humanPlayer ?
                (chosen is ArtifactCard ? playerArtifactArea : playerBattlefieldArea) :
                (chosen is ArtifactCard ? aiArtifactArea : aiBattlefieldArea));
        CardVisual visual = obj.GetComponent<CardVisual>();
        CardData data = CardDatabase.GetCardData(chosen.cardName);
        visual.Setup(chosen, this, data);
        visual.isInBattlefield = true;
        activeCardVisuals.Add(visual);
        visual.UpdateVisual();

        chosen.OnEnterPlay(player);
        if (chosen is CreatureCard)
            NotifyCreatureEntered(chosen, player);
        if (chosen is LandCard)
            NotifyLandEntered(chosen, player);
        if ((chosen is ArtifactCard) || (chosen is CreatureCard cc && cc.color.Contains("Artifact")))
            NotifyArtifactEntered(chosen, player);
        if (chosen is EnchantmentCard)
            NotifyEnchantmentEntered(chosen, player);

        ShuffleDeck(player);
        UpdateUI();
    }

    public IEnumerator RevealUntilCreature(Player player)
    {
        List<Card> revealedNonCreatures = new List<Card>();
        List<CardVisual> visuals = new List<CardVisual>();
        Card creatureCard = null;
        CardVisual creatureVisual = null;

        int index = 0;
        float spacing = 150f;

        while (player.Deck.Count > 0)
        {
            Card top = player.Deck[0];
            player.Deck.RemoveAt(0);

            GameObject obj = Instantiate(cardPrefab, stackZone);
            obj.transform.localPosition = new Vector3(index * spacing, 0f, 0f);
            CardVisual visual = obj.GetComponent<CardVisual>();
            CardData data = CardDatabase.GetCardData(top.cardName);
            visual.Setup(top, this, data);
            visual.isInStack = true;
            visuals.Add(visual);

            if (top is CreatureCard)
            {
                creatureCard = top;
                creatureVisual = visual;
                break;
            }
            else
            {
                revealedNonCreatures.Add(top);
            }

            index++;
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(0.5f);

        if (creatureCard != null)
        {
            player.Hand.Add(creatureCard);
            if (player == humanPlayer)
            {
                creatureVisual.transform.SetParent(playerHandArea, false);
                creatureVisual.isInStack = false;
                activeCardVisuals.Add(creatureVisual);
            }
            else
            {
                Destroy(creatureVisual.gameObject);
                if (enemyHandText != null)
                    enemyHandText.text = "Hand: " + player.Hand.Count;
            }
        }

        foreach (var card in revealedNonCreatures)
            player.Deck.Add(card);

        foreach (var visual in visuals)
        {
            if (!(visual.linkedCard is CreatureCard))
                Destroy(visual.gameObject);
        }

        ShuffleDeck(player);
        UpdateUI();
        pendingStackEffects = Mathf.Max(0, pendingStackEffects - 1);
    }

    public IEnumerator RevealUntilLand(Player player)
    {
        List<Card> revealedNonLands = new List<Card>();
        List<CardVisual> visuals = new List<CardVisual>();
        Card landCard = null;
        CardVisual landVisual = null;

        int index = 0;
        float spacing = 150f;

        while (player.Deck.Count > 0)
        {
            Card top = player.Deck[0];
            player.Deck.RemoveAt(0);

            GameObject obj = Instantiate(cardPrefab, stackZone);
            obj.transform.localPosition = new Vector3(index * spacing, 0f, 0f);
            CardVisual visual = obj.GetComponent<CardVisual>();
            CardData data = CardDatabase.GetCardData(top.cardName);
            visual.Setup(top, this, data);
            visual.isInStack = true;
            visuals.Add(visual);

            if (top is LandCard)
            {
                landCard = top;
                landVisual = visual;
                break;
            }
            else
            {
                revealedNonLands.Add(top);
            }

            index++;
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(0.5f);

        if (landCard != null)
        {
            player.Hand.Add(landCard);
            if (player == humanPlayer)
            {
                landVisual.transform.SetParent(playerHandArea, false);
                landVisual.isInStack = false;
                activeCardVisuals.Add(landVisual);
            }
            else
            {
                Destroy(landVisual.gameObject);
                if (enemyHandText != null)
                    enemyHandText.text = "Hand: " + player.Hand.Count;
            }
        }

        foreach (var card in revealedNonLands)
            player.Deck.Add(card);

        foreach (var visual in visuals)
        {
            if (!(visual.linkedCard is LandCard))
                Destroy(visual.gameObject);
        }

        ShuffleDeck(player);
        UpdateUI();
        pendingStackEffects = Mathf.Max(0, pendingStackEffects - 1);
    }

    public void ReturnRandomInstantOrSorceryFromGraveyard(Player player)
    {
        var spells = player.Graveyard
            .Where(card => card is SorceryCard)
            .ToList();
        if (spells.Count == 0)
            return;

        Card chosen = spells[Random.Range(0, spells.Count)];
        player.Graveyard.Remove(chosen);
        player.Hand.Add(chosen);

        if (player == humanPlayer)
        {
            GameObject obj = Instantiate(cardPrefab, playerHandArea);
            CardVisual visual = obj.GetComponent<CardVisual>();
            CardData data = CardDatabase.GetCardData(chosen.cardName);
            visual.Setup(chosen, this, data);
            activeCardVisuals.Add(visual);
        }
        else if (enemyHandText != null)
        {
            enemyHandText.text = "Hand: " + player.Hand.Count;
        }

        RefreshGraveyardVisuals(player);
        UpdateUI();
    }

    public void ReturnRandomCreatureFromGraveyardToBattlefield(Player player, int maxManaCost)
    {
        var creatures = player.Graveyard
            .OfType<CreatureCard>()
            .Where(c => c.manaCost <= maxManaCost)
            .ToList();
        if (creatures.Count == 0)
            return;

        Card chosen = creatures[Random.Range(0, creatures.Count)];
        player.Graveyard.Remove(chosen);
        player.Battlefield.Add(chosen);

        if (chosen is CreatureCard creature)
        {
            creature.hasSummoningSickness = true;
            if (creature.entersTapped || IsAllPermanentsEnterTappedActive())
            {
                creature.isTapped = true;
                Debug.Log($"{creature.cardName} enters tapped (due to static effect).");
            }
        }

        GameObject obj = Instantiate(cardPrefab, player == humanPlayer ? playerBattlefieldArea : aiBattlefieldArea);
        CardVisual visual = obj.GetComponent<CardVisual>();
        CardData data = CardDatabase.GetCardData(chosen.cardName);
        visual.Setup(chosen, this, data);
        visual.isInBattlefield = true;
        activeCardVisuals.Add(visual);
        visual.UpdateVisual();

        chosen.OnEnterPlay(player);
        if (chosen is CreatureCard)
            NotifyCreatureEntered(chosen, player);
        if (chosen is LandCard)
            NotifyLandEntered(chosen, player);
        if ((chosen is ArtifactCard) || (chosen is CreatureCard cc && cc.color.Contains("Artifact")))
            NotifyArtifactEntered(chosen, player);
        if (chosen is EnchantmentCard)
            NotifyEnchantmentEntered(chosen, player);

        RefreshGraveyardVisuals(player);
        UpdateUI();
    }

    public void ReturnRandomZombieFromGraveyardToBattlefield(Player player)
    {
        var zombies = player.Graveyard
            .OfType<CreatureCard>()
            .Where(c => c.subtypes.Contains("Zombie"))
            .ToList();
        if (zombies.Count == 0)
            return;

        Card chosen = zombies[Random.Range(0, zombies.Count)];
        player.Graveyard.Remove(chosen);
        player.Battlefield.Add(chosen);

        if (chosen is CreatureCard creature)
        {
            creature.hasSummoningSickness = true;
            if (creature.entersTapped || IsAllPermanentsEnterTappedActive())
            {
                creature.isTapped = true;
                Debug.Log($"{creature.cardName} enters tapped (due to static effect).");
            }
        }

        GameObject obj = Instantiate(cardPrefab, player == humanPlayer ? playerBattlefieldArea : aiBattlefieldArea);
        CardVisual visual = obj.GetComponent<CardVisual>();
        CardData data = CardDatabase.GetCardData(chosen.cardName);
        visual.Setup(chosen, this, data);
        visual.isInBattlefield = true;
        activeCardVisuals.Add(visual);
        visual.UpdateVisual();

        chosen.OnEnterPlay(player);
        if (chosen is CreatureCard)
            NotifyCreatureEntered(chosen, player);
        if (chosen is LandCard)
            NotifyLandEntered(chosen, player);
        if ((chosen is ArtifactCard) || (chosen is CreatureCard cc && cc.color.Contains("Artifact")))
            NotifyArtifactEntered(chosen, player);
        if (chosen is EnchantmentCard)
            NotifyEnchantmentEntered(chosen, player);

        RefreshGraveyardVisuals(player);
        UpdateUI();
    }

    public void ReturnCreatureFromGraveyardToBattlefield(Player player, CreatureCard creature)
    {
        if (creature == null || !player.Graveyard.Contains(creature))
            return;

        player.Graveyard.Remove(creature);
        player.Battlefield.Add(creature);

        creature.hasSummoningSickness = true;
        if (creature.entersTapped || IsAllPermanentsEnterTappedActive())
        {
            creature.isTapped = true;
            Debug.Log($"{creature.cardName} enters tapped from graveyard" +
                      (IsAllPermanentsEnterTappedActive() && !creature.entersTapped ?
                       " due to global effect" :
                       "") + ".");
        }

        GameObject obj = Instantiate(cardPrefab, player == humanPlayer ? playerBattlefieldArea : aiBattlefieldArea);
        CardVisual visual = obj.GetComponent<CardVisual>();
        CardData data = CardDatabase.GetCardData(creature.cardName);
        visual.Setup(creature, this, data);
        visual.isInBattlefield = true;
        activeCardVisuals.Add(visual);
        visual.UpdateVisual();

        creature.OnEnterPlay(player);
        NotifyCreatureEntered(creature, player);
        if ((creature is ArtifactCard) || creature.color.Contains("Artifact"))
            NotifyArtifactEntered(creature, player);

        RefreshGraveyardVisuals(player);
        if (graveyardViewActive && graveyardUIManager != null)
            graveyardUIManager.Open(player.Graveyard);
        UpdateUI();
    }

    public void ReturnCreatureFromGraveyardToHand(Player player, CreatureCard creature)
    {
        if (creature == null || !player.Graveyard.Contains(creature))
            return;

        player.Graveyard.Remove(creature);
        player.Hand.Add(creature);

        if (player == humanPlayer)
        {
            GameObject obj = Instantiate(cardPrefab, playerHandArea);
            CardVisual visual = obj.GetComponent<CardVisual>();
            CardData data = CardDatabase.GetCardData(creature.cardName);
            visual.Setup(creature, this, data);
            activeCardVisuals.Add(visual);
        }

        RefreshGraveyardVisuals(player);
        if (graveyardViewActive && graveyardUIManager != null)
            graveyardUIManager.Open(player.Graveyard);
        UpdateUI();
    }

    public void TapCardForMana(CreatureCard creature)
    {
        if (!creature.isTapped)
        {
            creature.isTapped = true;
            humanPlayer.ColoredMana.Colorless++;
            UpdateUI();
        }
    }

    public void TapToLoseLife(CreatureCard creature)
    {
        if (creature.isTapped || creature.hasSummoningSickness)
            return;

        creature.isTapped = true;

        Player owner = GetOwnerOfCard(creature);            // FIXED: declare 'owner'
        Player opponent = GetOpponentOf(owner);

        opponent.Life -= creature.tapLifeLossAmount;
        Debug.Log($"{creature.cardName} tapped: opponent loses {creature.tapLifeLossAmount} life.");

        SoundManager.Instance.PlaySound(SoundManager.Instance.plague);
        ShowBloodSplatVFX(creature);

        VisualEffectManager.Instance.ShowFloatingDamageForPlayer(creature.tapLifeLossAmount, owner != humanPlayer);

        UpdateUI();
        CheckForGameEnd();
    }

    public void ShowBloodSplatVFX(Card card)
    {
        Debug.Log("ShowBloodSplatVFX triggered on: " + card.cardName);

        CardVisual visual = FindCardVisual(card);
        if (visual == null)
        {
            Debug.LogWarning("No visual found for card " + card.cardName);
            return;
        }

        Vector3 spawnPos = visual.transform.position;
        spawnPos.z = 0f;
        spawnPos += new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), 0);

        GameObject vfx = Instantiate(bloodSplatPrefab, spawnPos, Quaternion.identity);
        Destroy(vfx, 1.5f);
        Debug.Log("Spawned blood VFX at " + spawnPos);
    }

    public void PayToGainAbility(CreatureCard creature)
    {
        if (creature.isTapped) return;
        if (creature.temporaryKeywordAbilities.Contains(creature.abilityToGain))
        {
            Debug.Log($"{creature.cardName} already gained {creature.abilityToGain} this turn.");
            return;
        }

        Player owner = GetOwnerOfCard(creature);
        int remaining = creature.manaToPayToActivate;

        if (owner.ColoredMana.Total() >= remaining)
        {
            // Spend colorless first
            int useColorless = Mathf.Min(owner.ColoredMana.Colorless, remaining);
            owner.ColoredMana.Colorless -= useColorless;
            remaining -= useColorless;

            // Spend from WUBRG
            remaining -= Player.ManaPool.SpendFromPool(ref owner.ColoredMana.White, remaining);
            remaining -= Player.ManaPool.SpendFromPool(ref owner.ColoredMana.Blue, remaining);
            remaining -= Player.ManaPool.SpendFromPool(ref owner.ColoredMana.Black, remaining);
            remaining -= Player.ManaPool.SpendFromPool(ref owner.ColoredMana.Red, remaining);
            remaining -= Player.ManaPool.SpendFromPool(ref owner.ColoredMana.Green, remaining);

            if (remaining > 0)
            {
                Debug.LogWarning("PayToGainAbility: Not enough mana despite Total() check.");
                return;
            }

            if (!creature.keywordAbilities.Contains(creature.abilityToGain))
                creature.keywordAbilities.Add(creature.abilityToGain);
            if (!creature.temporaryKeywordAbilities.Contains(creature.abilityToGain))
                creature.temporaryKeywordAbilities.Add(creature.abilityToGain);
            Debug.Log($"{creature.cardName} gains {creature.abilityToGain} until end of turn.");
            var visual = FindCardVisual(creature);
            if (visual != null)
            {
                visual.UpdateVisual();
                if (CardHoverPreview.Instance != null &&
                    CardHoverPreview.Instance.CurrentCard == creature)
                {
                    CardHoverPreview.Instance.ShowCard(creature);
                }
            }
            UpdateUI();
        }
        else
        {
            Debug.Log($"Not enough mana to activate {creature.cardName}'s ability.");
            return;
        }
    }

    public void PayToBuffSelf(CreatureCard creature)
    {
        Player owner = GetOwnerOfCard(creature);
        int remaining = creature.manaToPayToActivate;

        if (owner.ColoredMana.Total() >= remaining)
        {
            int useColorless = Mathf.Min(owner.ColoredMana.Colorless, remaining);
            owner.ColoredMana.Colorless -= useColorless;
            remaining -= useColorless;

            remaining -= Player.ManaPool.SpendFromPool(ref owner.ColoredMana.White, remaining);
            remaining -= Player.ManaPool.SpendFromPool(ref owner.ColoredMana.Blue, remaining);
            remaining -= Player.ManaPool.SpendFromPool(ref owner.ColoredMana.Black, remaining);
            remaining -= Player.ManaPool.SpendFromPool(ref owner.ColoredMana.Red, remaining);
            remaining -= Player.ManaPool.SpendFromPool(ref owner.ColoredMana.Green, remaining);

            if (remaining > 0)
            {
                Debug.LogWarning("PayToBuffSelf: Not enough mana despite Total() check.");
                return;
            }

            creature.AddTemporaryBuff(1, 0);
            var vis = FindCardVisual(creature);
            if (vis != null)
                vis.UpdateVisual();

            Debug.Log($"{creature.cardName} gets +1/+0 until end of turn.");
            UpdateUI();
        }
        else
        {
            Debug.Log($"Not enough mana to activate {creature.cardName}'s ability.");
        }
    }

    public Player GetOwnerOfCard(Card card)
    {
        if (humanPlayer.Battlefield.Contains(card) ||
            humanPlayer.Hand.Contains(card) ||
            humanPlayer.Graveyard.Contains(card) ||
            humanPlayer.Deck.Contains(card))
        {
            return humanPlayer;
        }

        if (aiPlayer.Battlefield.Contains(card) ||
            aiPlayer.Hand.Contains(card) ||
            aiPlayer.Graveyard.Contains(card) ||
            aiPlayer.Deck.Contains(card))
        {
            return aiPlayer;
        }

        if (card.owner != null)
            return card.owner;

        Debug.LogWarning($"[GetOwnerOfCard] Couldn't find owner of {card.cardName}");
        return null;
    }

    public Player GetControllerOfCard(Card card)
    {
        if (humanPlayer.Battlefield.Contains(card))
            return humanPlayer;
        if (aiPlayer.Battlefield.Contains(card))
            return aiPlayer;
        return null;
    }

    public void ChangeController(Card card, Player newController)
    {
        Player current = GetControllerOfCard(card);
        if (current == newController || newController == null || current == null)
            return;

        current.Battlefield.Remove(card);
        newController.Battlefield.Add(card);

        CardVisual visual = FindCardVisual(card);
        if (visual != null)
        {
            Transform area;
            if (card is LandCard)
                area = newController == humanPlayer ? playerLandArea : aiLandArea;
            else if (card is ArtifactCard)
                area = newController == humanPlayer ? playerArtifactArea : aiArtifactArea;
            else if (card is EnchantmentCard)
                area = newController == humanPlayer ? playerEnchantmentArea : aiEnchantmentArea;
            else
                area = newController == humanPlayer ? playerBattlefieldArea : aiBattlefieldArea;

            visual.transform.SetParent(area, false);
            visual.UpdateVisual();
        }

        UpdateUI();
    }

    public void UpdateUI()
    {
        foreach (var visual in activeCardVisuals)
        {
            if (visual.isInBattlefield)
                visual.UpdateVisual();
        }

        if (enemyHandText != null)
            enemyHandText.text = "Hand: " + aiPlayer.Hand.Count;

        if (playerLifeText != null)
            playerLifeText.text = "" + humanPlayer.Life;

        if (enemyLifeText != null)
            enemyLifeText.text = "" + aiPlayer.Life;

        if (playerDeckCountText != null)
            playerDeckCountText.text = "" + humanPlayer.Deck.Count;
        if (playerGraveyardCountText != null)
            playerGraveyardCountText.text = "" + humanPlayer.Graveyard.Count;
        if (enemyDeckCountText != null)
            enemyDeckCountText.text = "" + aiPlayer.Deck.Count;
        if (enemyGraveyardCountText != null)
            enemyGraveyardCountText.text = "" + aiPlayer.Graveyard.Count;

        if (manaPoolText != null)
        {
            // Replace mana text with icon updates
            UpdateManaIcon(whiteManaIcon, whiteManaText, humanPlayer.ColoredMana.White);
            UpdateManaIcon(blueManaIcon, blueManaText, humanPlayer.ColoredMana.Blue);
            UpdateManaIcon(blackManaIcon, blackManaText, humanPlayer.ColoredMana.Black);
            UpdateManaIcon(redManaIcon, redManaText, humanPlayer.ColoredMana.Red);
            UpdateManaIcon(greenManaIcon, greenManaText, humanPlayer.ColoredMana.Green);
            UpdateManaIcon(colorlessManaIcon, colorlessManaText, humanPlayer.ColoredMana.Colorless);
        }
    }

    private void UpdateManaIcon(Image icon, TMP_Text label, int amount)
    {
        if (icon != null)
            icon.color = (amount > 0) ? Color.white : Color.black;

        if (label != null)
            label.text = amount.ToString();
    }

    // Ensures the graveyard count text stays above newly added cards
    private void EnsureGraveyardCounterOnTop(Player owner)
    {
        if (owner == humanPlayer)
        {
            if (playerGraveyardCountText != null)
                playerGraveyardCountText.transform.SetAsLastSibling();
        }
        else if (owner == aiPlayer)
        {
            if (enemyGraveyardCountText != null)
                enemyGraveyardCountText.transform.SetAsLastSibling();
        }
    }

    public void RefreshGraveyardVisuals(Player player)
    {
        var graveyardVisuals = activeCardVisuals
            .Where(cv => cv.isInGraveyard && GetOwnerOfCard(cv.linkedCard) == player)
            .ToList();

        foreach (var visual in graveyardVisuals)
        {
            activeCardVisuals.Remove(visual);
            Destroy(visual.gameObject);
        }

        foreach (var card in player.Graveyard)
        {
            if (card.isToken) continue;

            GameObject visualGO = Instantiate(cardPrefab,
                player == humanPlayer ? playerGraveyardArea : aiGraveyardArea);
            CardVisual graveyardVisual = visualGO.GetComponent<CardVisual>();
            graveyardVisual.Setup(card, this);
            graveyardVisual.transform.localPosition = Vector3.zero;
            graveyardVisual.UpdateGraveyardVisual();
            // Keep UI overlay elements above the cards
            // Newest card should appear on top
            graveyardVisual.transform.SetAsLastSibling();
            EnsureGraveyardCounterOnTop(player);

            activeCardVisuals.Add(graveyardVisual);
        }
    }

    public void ShowPlayerGraveyard()
    {
        if (graveyardUIManager != null)
            graveyardUIManager.Open(humanPlayer.Graveyard);
    }

    public void ShowOpponentGraveyard()
    {
        if (graveyardUIManager != null)
            graveyardUIManager.Open(aiPlayer.Graveyard);
    }

    public void ClosePlayerGraveyard()
    {
        if (graveyardUIManager != null)
            graveyardUIManager.Close();
    }

    public void WinBattle()
    {
        if (!string.IsNullOrEmpty(BattleData.CurrentZoneId))
        {
            Debug.Log("Player won battle at zone ID: " + BattleData.CurrentZoneId);
            PlayerPrefs.SetString("LastCompletedZone", BattleData.CurrentZoneId);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogWarning("No zone ID found when trying to WinBattle.");
        }

        // Flag the merchant inventory to refresh on the next visit
        PlayerPrefs.SetInt("RefreshMerchant", 1);
        PlayerPrefs.Save();

        SceneManager.LoadScene("MapScene");
    }

    void PickRandomBeginnerDeck(Player ai)
    {
        var decks = new System.Action<Player>[]
        {
                    DeckDatabase.BuildWhiteBeginnerDeck,
                    DeckDatabase.BuildBlueBeginnerDeck,
                    DeckDatabase.BuildBlackBeginnerDeck,
                    DeckDatabase.BuildRedBeginnerDeck,
                    DeckDatabase.BuildGreenBeginnerDeck,
                    DeckDatabase.BuildRuinsDeck
        };

        decks[Random.Range(0, decks.Length)](ai);
    }

    void PickRandomAdvancedDeck(Player ai)
    {
        var decks = new System.Action<Player>[]
        {
                    DeckDatabase.BuildWhiteAdvancedDeck,
                    DeckDatabase.BuildBlueAdvancedDeck,
                    DeckDatabase.BuildBlackAdvancedDeck,
                    DeckDatabase.BuildRedAdvancedDeck,
                    DeckDatabase.BuildGreenAdvancedDeck
        };

        decks[Random.Range(0, decks.Length)](ai);
    }

    void LoadDeckByKey(Player ai, string key)
    {
        switch (key)
        {
            // STARTER + BOSS
            case "Deck_Starter":
                DeckDatabase.BuildStarterDeck(ai);
                break;
            case "Deck_Boss":
                DeckDatabase.BuildBossDeck(ai);
                break;

            // BEGINNER (6)
            case "Deck_Shore":
                DeckDatabase.BuildBlueBeginnerDeck(ai);
                break;
            case "Deck_Camp":
                DeckDatabase.BuildRedBeginnerDeck(ai);
                break;
            case "Deck_Graveyard":
                DeckDatabase.BuildBlackBeginnerDeck(ai);
                break;
            case "Deck_Thicket":
                DeckDatabase.BuildGreenBeginnerDeck(ai);
                break;
            case "Deck_Village":
                DeckDatabase.BuildWhiteBeginnerDeck(ai);
                break;
            case "Deck_Ruins":
                DeckDatabase.BuildRuinsDeck(ai);
                break;

            // ADVANCED (5)
            case "Deck_Church":
                DeckDatabase.BuildWhiteAdvancedDeck(ai);
                break;
            case "Deck_Tower":
                DeckDatabase.BuildBlueAdvancedDeck(ai);
                break;
            case "Deck_Hut":
                DeckDatabase.BuildBlackAdvancedDeck(ai);
                break;
            case "Deck_Nest":
                DeckDatabase.BuildRedAdvancedDeck(ai);
                break;
            case "Deck_Woods":
                DeckDatabase.BuildGreenAdvancedDeck(ai);
                break;

            default:
                Debug.LogWarning("Unknown deck key: " + key + " — using fallback.");
                DeckDatabase.BuildStarterDeck(ai);
                break;
        }
    }

    public bool IsAllPermanentsEnterTappedActive()
    {
        return humanPlayer.Battlefield.Concat(aiPlayer.Battlefield)
            .Any(card => card.keywordAbilities != null &&
                        card.keywordAbilities.Contains(KeywordAbility.AllPermanentsEnterTapped));
    }

    public bool IsLifeGainPrevented()
    {
        return humanPlayer.Battlefield.Concat(aiPlayer.Battlefield)
            .Any(card => card.keywordAbilities != null &&
                        card.keywordAbilities.Contains(KeywordAbility.NoLifeGain));
    }

    public bool IsOnlyCastCreatureSpellsActive()
    {
        return humanPlayer.Battlefield.Concat(aiPlayer.Battlefield)
            .Any(card => card.keywordAbilities != null &&
                        card.keywordAbilities.Contains(KeywordAbility.OnlyCastCreatureSpells));
    }

    public bool IsHasteCreaturesOnlyBlockedByHasteActive(Player player)
    {
        return player.Battlefield.Any(card => card.keywordAbilities != null &&
            card.keywordAbilities.Contains(KeywordAbility.HasteCreaturesOnlyBlockedByHaste));
    }

    public int GetCreatureCostReduction(Player player)
    {
        return player.Battlefield.Count(card => card.keywordAbilities != null &&
            card.keywordAbilities.Contains(KeywordAbility.CreatureSpellsCostOneLess));
    }

    public int GetBeastCreatureCostReduction(Player player)
    {
        return player.Battlefield.Count(card => card.keywordAbilities != null &&
            card.keywordAbilities.Contains(KeywordAbility.BeastCreatureSpellsCostOneLess));
    }

    public int GetPotionCostReduction(Player player)
    {
        return player.Battlefield.Count(card => card.keywordAbilities != null &&
            card.keywordAbilities.Contains(KeywordAbility.PotionSpellsCostOneLess));
    }

    public int GetOpponentSpellTax(Player player)
    {
        Player opponent = GetOpponentOf(player);
        return opponent.Battlefield.Count(card => card.keywordAbilities != null &&
            card.keywordAbilities.Contains(KeywordAbility.OpponentSpellsCostOneMore));
    }

    public void TryGainLife(Player player, int amount, bool showVFX = true)
    {
        if (amount <= 0 || IsLifeGainPrevented())
            return;

        player.Life += amount;
        UpdateUI();

        NotifyLifeGain(player, amount);

        if (showVFX)
        {
            VisualEffectManager.Instance.ShowFloatingHealForPlayer(amount, player == humanPlayer);
        }
    }

    public void LoseLife(Player player, int amount, bool showVFX = true)
    {
        player.ChangeLife(amount);
        UpdateUI();

        if (showVFX)
        {
            VisualEffectManager.Instance.ShowFloatingDamageForPlayer(amount, player == humanPlayer);

        }
    }

    public void BeginTargetSelection(SorceryCard sorcery, Player caster, CardVisual visual)
    {
        targetingSorcery = sorcery;
        targetingPlayer = caster;
        targetingVisual = visual;
        isTargetingMode = true;

        if (!sorcery.requiresTarget)
        {
            Debug.LogWarning("BeginTargetSelection called for non-targeting sorcery.");
            return;
        }

        // Highlight the selected card
        if (visual != null)
            visual.EnableTargetingHighlight(true);

        // Check if any valid targets exist (but do not highlight anything)
        foreach (var cv in activeCardVisuals)
        {
            if (cv == null || cv.linkedCard == null)
                continue;

            Card target = cv.linkedCard;

            bool correctType =
                (sorcery.requiredTargetType == SorceryCard.TargetType.Creature && target is CreatureCard) ||
                (sorcery.requiredTargetType == SorceryCard.TargetType.TappedCreature && target is CreatureCard tc && tc.isTapped) ||
                (sorcery.requiredTargetType == SorceryCard.TargetType.Land && target is LandCard) ||
                (sorcery.requiredTargetType == SorceryCard.TargetType.Artifact && target is ArtifactCard) ||
                (sorcery.requiredTargetType == SorceryCard.TargetType.Enchantment && target is EnchantmentCard) ||
                (sorcery.requiredTargetType == SorceryCard.TargetType.CreatureOrPlayer && target is CreatureCard);

            bool isOnBattlefield = GetOwnerOfCard(target)?.Battlefield.Contains(target) == true;

            bool colorMatches = true;
            if (!string.IsNullOrEmpty(sorcery.requiredTargetColor))
            {
                CardData data = CardDatabase.GetCardData(target.cardName);
                colorMatches = data != null && data.color.Contains(targetingSorcery.requiredTargetColor);
            }

            if (correctType && isOnBattlefield && colorMatches && !IsProtectedFromSpell(target))
            {
                // Valid target exists, but no visual feedback is shown
            }
        }
    }

    private IEnumerator ResolveTargetedSorceryAfterDelay(Card target, Player caster, SorceryCard sorcery, CardVisual visual)
    {
        yield return new WaitForSeconds(2f);

        // The card-specific ResolveEffect(target) already invokes the general
        // ResolveEffect method internally. Calling it again here caused cards
        // such as Forced Mummification to create two zombies and Stain of Rot
        // to make the opponent lose 4 life instead of 2. Run it only once.
        sorcery.ResolveEffect(caster, target);
        SendToGraveyard(sorcery, caster, fromStack: true);

        if (caster == aiPlayer && visual != null)
        {
            activeCardVisuals.Remove(visual);
            Destroy(visual.gameObject);
        }

        UpdateUI();
        isStackBusy = false;
    }

    public void CompleteTargetSelection(CardVisual targetVisual)
    {
        Card chosen = targetVisual.linkedCard;

        // Artifact damage ability
        if (targetingArtifact != null &&
            targetingArtifact.activatedAbilities.Contains(ActivatedAbility.DealDamageToCreature))
        {
            if (chosen is CreatureCard targetCreature &&
                GetOwnerOfCard(targetCreature)?.Battlefield.Contains(targetCreature) == true)
            {
                Player controller = targetingPlayer;
                int remaining = targetingArtifact.manaToPayToActivate;

                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Colorless, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.White, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Blue, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Black, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Red, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Green, remaining);

                if (remaining > 0)
                {
                    Debug.LogWarning("Not enough mana to activate artifact.");
                    CancelTargeting();
                    return;
                }

                ArtifactCard artifact = targetingArtifact;
                targetingArtifact.isTapped = true;
                SendToGraveyard(targetingArtifact, controller);
                QueueArtifactActivatedAbility(artifact, ActivatedAbility.DealDamageToCreature, controller, targetCreature);
                UpdateUI();
            }
            else
            {
                Debug.Log("Invalid target. Artifact effect canceled.");
                targetingArtifact.isTapped = false;
            }

            targetingArtifact = null;
            targetingPlayer = null;
            targetingVisual = null;
            isTargetingMode = false;
            return;
        }
        // Artifact buff ability
        if (targetingArtifact != null &&
            targetingArtifact.activatedAbilities.Contains(ActivatedAbility.BuffTargetCreature))
        {
            if (chosen is CreatureCard targetCreature &&
                GetOwnerOfCard(targetCreature)?.Battlefield.Contains(targetCreature) == true)
            {
                Player controller = targetingPlayer;
                int remaining = targetingArtifact.manaToPayToActivate;

                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Colorless, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.White, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Blue, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Black, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Red, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Green, remaining);

                if (remaining > 0)
                {
                    Debug.LogWarning("Not enough mana to activate artifact.");
                    CancelTargeting();
                    return;
                }

                ArtifactCard artifact = targetingArtifact;
                targetingArtifact.isTapped = true;
                SendToGraveyard(targetingArtifact, controller);
                QueueArtifactActivatedAbility(artifact, ActivatedAbility.BuffTargetCreature, controller, targetCreature);
                UpdateUI();
            }
            else
            {
                Debug.Log("Invalid target. Artifact effect canceled.");
                targetingArtifact.isTapped = false;
            }

            targetingArtifact = null;
            targetingPlayer = null;
            targetingVisual = null;
            isTargetingMode = false;
            return;
        }
        // Equipment equip ability
        if (targetingEquipment != null &&
            targetingEquipment.activatedAbilities.Contains(ActivatedAbility.Equip))
        {
            if (chosen is CreatureCard targetCreature &&
                GetOwnerOfCard(targetCreature) == targetingPlayer &&
                targetingPlayer.Battlefield.Contains(targetCreature))
            {
                Player controller = targetingPlayer;
                int remaining = targetingEquipment.EquipCost;

                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Colorless, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.White, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Blue, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Black, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Red, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref controller.ColoredMana.Green, remaining);

                if (remaining > 0)
                {
                    Debug.LogWarning("Not enough mana to equip artifact.");
                    CancelTargeting();
                    return;
                }

                targetingEquipment.Equip(targetCreature);

                UpdateUI();
            }
            else
            {
                Debug.Log("Invalid target for equipment.");
            }

            targetingEquipment = null;
            targetingPlayer = null;
            targetingVisual = null;
            isTargetingMode = false;
            return;
        }
        // Aura casting
        if (targetingAura != null)
        {
            Card targetCard = chosen;
            bool correctType =
                (targetingAura.requiredTargetType == SorceryCard.TargetType.Creature && targetCard is CreatureCard) ||
                (targetingAura.requiredTargetType == SorceryCard.TargetType.TappedCreature && targetCard is CreatureCard tc && tc.isTapped);
            Player targetOwner = GetOwnerOfCard(targetCard);
            bool isOnBattlefield = targetOwner?.Battlefield.Contains(targetCard) == true;
            bool correctController = !targetingAura.targetMustBeControlledCreature || targetOwner == targetingPlayer;

            if (!correctType || !isOnBattlefield || !correctController)
            {
                Debug.Log("Invalid target for aura.");
                CancelTargeting();
                return;
            }

            var cost = GetManaCostBreakdown(targetingAura.manaCost, targetingAura.color);
            int tax = GetOpponentSpellTax(targetingPlayer);
            if (tax > 0)
            {
                if (!cost.ContainsKey("Colorless"))
                    cost["Colorless"] = 0;
                cost["Colorless"] += tax;
            }
            if (!targetingPlayer.ColoredMana.CanPay(cost))
            {
                Debug.LogWarning("Not enough mana to cast aura.");
                CancelTargeting();
                return;
            }

            isStackBusy = true;
            targetingPlayer.ColoredMana.Pay(cost);
            targetingPlayer.Hand.Remove(targetingAura);

            targetingAura.owner = targetingPlayer;
            targetingAura.attachedTo = targetCard;
            UpdateUI();

            if (targetingVisual != null)
            {
                targetingVisual.transform.SetParent(stackZone, false);
                targetingVisual.isInStack = true;
                targetingVisual.transform.localPosition = Vector3.zero;
                targetingVisual.transform.localRotation = Quaternion.identity;
                targetingVisual.transform.localScale = Vector3.one;
                targetingVisual.EnableTargetingHighlight(false);
            }
            SoundManager.Instance.PlaySound(SoundManager.Instance.cardPlay);

            StartCoroutine(ResolveAuraAfterDelay(targetingAura, targetingVisual, targetingPlayer));

            targetingAura = null;
            targetingPlayer = null;
            targetingVisual = null;
            isTargetingMode = false;
            return;
        }
        // Creature ETB targeting
        if (targetingCreature != null && targetingAbility != null)
        {
            Card target = targetVisual.linkedCard;

            bool correctType =
                (targetingAbility.requiredTargetType == SorceryCard.TargetType.Creature && target is CreatureCard creature &&
                    !(targetingAbility.excludeArtifactCreatures && creature.color.Contains("Artifact"))) ||
                (targetingAbility.requiredTargetType == SorceryCard.TargetType.Land && target is LandCard) ||
                (targetingAbility.requiredTargetType == SorceryCard.TargetType.Artifact && target is ArtifactCard) ||
                (targetingAbility.requiredTargetType == SorceryCard.TargetType.Enchantment && target is EnchantmentCard);

            bool isOnBattlefield = GetOwnerOfCard(target)?.Battlefield.Contains(target) == true;

            if (!correctType || !isOnBattlefield)
            {
                Debug.LogWarning($"Invalid target: {target.cardName} does not match ETB type.");
                CancelTargeting();
                return;
            }

            Debug.Log($"ETB target selected: {target.cardName}");
            targetingAbility.effect?.Invoke(GetOwnerOfCard(targetingCreature), target); // You'll update effect type later if needed

            UpdateUI();
            CheckDeaths(humanPlayer);
            CheckDeaths(aiPlayer);

            targetingCreature = null;
            targetingAbility = null;
            targetingVisual = null;
            isTargetingMode = false;
            return;
        }
        // Sorcery fallback
        if (targetingSorcery != null)
        {
            // Validate type
            bool correctType =
                (targetingSorcery.requiredTargetType == SorceryCard.TargetType.Creature && chosen is CreatureCard creatureT &&
                    !(targetingSorcery.excludeArtifactCreatures && creatureT.color.Contains("Artifact"))) ||
                (targetingSorcery.requiredTargetType == SorceryCard.TargetType.Land && chosen is LandCard) ||
                (targetingSorcery.requiredTargetType == SorceryCard.TargetType.Artifact && chosen is ArtifactCard) ||
                (targetingSorcery.requiredTargetType == SorceryCard.TargetType.Enchantment && chosen is EnchantmentCard) ||
                (targetingSorcery.requiredTargetType == SorceryCard.TargetType.CreatureOrPlayer && chosen is CreatureCard);

            // Validate color
            bool colorMatches = true;
            if (!string.IsNullOrEmpty(targetingSorcery.requiredTargetColor))
            {
                CardData data = CardDatabase.GetCardData(chosen.cardName);
                colorMatches = data != null && data.color.Contains(targetingSorcery.requiredTargetColor);
            }

            if (!correctType || !colorMatches)
            {
                Debug.LogWarning($"Invalid target: {chosen.cardName} does not match type or color requirements.");
                return;
            }

            // Pay mana before resolving
            var cost = GetManaCostBreakdown(targetingSorcery.manaCost, targetingSorcery.color);
            int tax = GetOpponentSpellTax(targetingPlayer);
            if (tax > 0)
            {
                if (!cost.ContainsKey("Colorless"))
                    cost["Colorless"] = 0;
                cost["Colorless"] += tax;
            }
            if (!targetingPlayer.ColoredMana.CanPay(cost))
            {
                Debug.LogWarning("Not enough mana to cast targeted sorcery.");
                CancelTargeting();
                return;
            }

            targetingPlayer.ColoredMana.Pay(cost);
            if (targetingSorcery.hasXCost)
            {
                targetingSorcery.xValue = targetingPlayer.ColoredMana.Total();
                if (targetingSorcery.xValue > 0)
                    targetingPlayer.ColoredMana.SpendGeneric(targetingSorcery.xValue);
            }
            targetingPlayer.Hand.Remove(targetingSorcery);
            UpdateUI();

            targetingSorcery.chosenTarget = chosen;

            Debug.Log($"Target selected: {chosen.cardName}");

            targetingVisual.transform.SetParent(stackZone, false);
            targetingVisual.isInStack = true;
            targetingVisual.transform.localPosition = Vector3.zero;
            targetingVisual.transform.localRotation = Quaternion.identity;
            targetingVisual.transform.localScale = Vector3.one;
            SoundManager.Instance.PlaySound(SoundManager.Instance.cardPlay);

            if (targetingVisual != null)
                targetingVisual.EnableTargetingHighlight(false);

            StartCoroutine(ResolveTargetedSorceryAfterDelay(chosen, targetingPlayer, targetingSorcery, targetingVisual));

            targetingSorcery = null;
            targetingPlayer = null;
            targetingVisual = null;
            isTargetingMode = false;
        }
    }

    public void CancelTargeting()
    {
        foreach (var cv in activeCardVisuals)
            cv.EnableTargetingHighlight(false); // turn off all

        if (targetingArtifact != null)
        {
            targetingArtifact.isTapped = false; // untap if ability was aborted
            FindCardVisual(targetingArtifact)?.UpdateVisual();
        }

        targetingArtifact = null;
        targetingEquipment = null;
        targetingSorcery = null;
        targetingAura = null;
        targetingPlayer = null;

        if (targetingVisual != null)
            targetingVisual.EnableTargetingHighlight(false); // turn off highlight

        targetingVisual = null;
        isTargetingMode = false;
        isStackBusy = false;

        UpdateUI();
    }

    public void CompletePlayerTargetSelection(Player targetPlayer)
    {
        // Edge case: accidentally triggered for non-sorcery
        if (targetingSorcery == null)
        {
            Debug.LogWarning("CompletePlayerTargetSelection called but no sorcery is being resolved.");
            isTargetingMode = false;
            targetingPlayer = null;
            targetingVisual = null;
            isStackBusy = false;
            UpdateUI();
            return;
        }

        if (targetingVisual != null)
            targetingVisual.EnableTargetingHighlight(false);

        // Move visual to stack
        targetingVisual.transform.SetParent(stackZone, false);
        targetingVisual.isInStack = true;
        targetingVisual.transform.localPosition = Vector3.zero;
        targetingVisual.transform.localRotation = Quaternion.identity;
        targetingVisual.transform.localScale = Vector3.one;
        SoundManager.Instance.PlaySound(SoundManager.Instance.cardPlay);

        StartCoroutine(ResolveTargetedSorceryOnPlayerAfterDelay(targetPlayer, targetingPlayer, targetingSorcery, targetingVisual));

        if (targetingPlayer == aiPlayer && targetingVisual != null)
        {
            activeCardVisuals.Remove(targetingVisual);
            Destroy(targetingVisual.gameObject);
        }

        isTargetingMode = false;
        targetingSorcery = null;
        targetingPlayer = null;
        targetingVisual = null;

        UpdateUI();
        isStackBusy = false;
    }

    private bool IsProtectedFromSpell(Card card)
    {
        if (card is CreatureCard creature && targetingSorcery != null)
        {
            KeywordAbility protection = ProtectionUtils.GetProtectionKeyword(targetingSorcery.PrimaryColor);
            return creature.keywordAbilities.Contains(protection);
        }

        return false;
    }

    private IEnumerator ResolveTargetedSorceryOnPlayerAfterDelay(Player targetPlayer, Player caster, SorceryCard sorcery, CardVisual visual)
    {
        yield return new WaitForSeconds(2f);

        sorcery.ResolveEffectOnPlayer(caster, targetPlayer);
        SendToGraveyard(sorcery, caster, fromStack: true);

        if (caster == aiPlayer && visual != null)
        {
            activeCardVisuals.Remove(visual);
            Destroy(visual.gameObject);
        }

        UpdateUI();
        isStackBusy = false;
    }

    public Dictionary<string, int> GetManaCostBreakdown(int totalCost, List<string> color)
    {
        Dictionary<string, int> breakdown = new Dictionary<string, int>();

        // Treat empty color or "Artifact" as fully colorless
        if (color == null || color.Count == 0 || (color.Count == 1 && color[0] == "Artifact"))
        {
            breakdown["Colorless"] = totalCost;
        }
        else
        {
            foreach (string c in color)
            {
                if (c == "Artifact") continue; // Don't treat Artifact as colored mana
                if (!breakdown.ContainsKey(c))
                    breakdown[c] = 0;
                breakdown[c]++;
            }

            int coloredCount = breakdown.Values.Sum();
            int generic = totalCost - coloredCount;
            if (generic > 0)
                breakdown["Colorless"] = generic;
        }

        return breakdown;
    }


    public void BeginTargetingWithArtifactDamage(ArtifactCard artifact, Player player, CardVisual visual)
    {
        targetingArtifact = artifact; // << Store the artifact being used
        targetingSorcery = null;
        targetingPlayer = player;
        targetingVisual = visual;
        isTargetingMode = true;

        artifact.isTapped = true; // show the potion tapped while selecting target
        FindCardVisual(artifact)?.UpdateVisual();

        Debug.Log("Targeting creature to deal damage with artifact.");
    }

    public void BeginTargetingWithArtifactBuff(ArtifactCard artifact, Player player, CardVisual visual)
    {
        targetingArtifact = artifact;
        targetingSorcery = null;
        targetingAura = null;
        targetingPlayer = player;
        targetingVisual = visual;
        isTargetingMode = true;

        artifact.isTapped = true; // show the potion tapped while selecting target
        FindCardVisual(artifact)?.UpdateVisual();

        Debug.Log("Targeting creature to buff with artifact.");
    }

    public void BeginAuraTargetSelection(AuraCard aura, Player caster, CardVisual visual)
    {
        targetingAura = aura;
        targetingSorcery = null;
        targetingArtifact = null;
        targetingEquipment = null;
        targetingPlayer = caster;
        targetingVisual = visual;
        isTargetingMode = true;

        // Highlight the aura being cast so the player has feedback similar to
        // sorceries while choosing a target
        if (visual != null)
            visual.EnableTargetingHighlight(true);
    }

    public void BeginEquipmentTargetSelection(EquipmentCard equipment, Player player, CardVisual visual)
    {
        targetingEquipment = equipment;
        targetingArtifact = null;
        targetingSorcery = null;
        targetingAura = null;
        targetingPlayer = player;
        targetingVisual = visual;
        isTargetingMode = true;
    }

    public IEnumerator ResolveArtifactDamageAfterDelay(CardVisual targetVisual, Card targetCard)
    {
        yield return new WaitForSeconds(0.4f);

        if (targetCard is CreatureCard creature &&
            GetOwnerOfCard(creature)?.Battlefield.Contains(creature) == true &&
            targetingArtifact != null)
        {
            creature.TakeDamage(targetingArtifact.damageToCreature);
            Card asCard = targetingArtifact;
            if (asCard is CreatureCard srcCreature &&
                srcCreature.keywordAbilities.Contains(KeywordAbility.Deathtouch) &&
                targetingArtifact.damageToCreature > 0)
            {
                creature.Kill();
            }
            Debug.Log($"{targetingArtifact.cardName} dealt {targetingArtifact.damageToCreature} damage to {creature.cardName}");
            targetingArtifact.isTapped = true;
            SendToGraveyard(targetingArtifact, targetingPlayer);
            CheckDeaths(humanPlayer);
            CheckDeaths(aiPlayer);
            UpdateUI();
        }
        else
        {
            Debug.Log("Invalid or missing target — damage not applied.");
            targetingArtifact.isTapped = false; // Optionally untap
        }

        targetingArtifact = null;
        targetingPlayer = null;
        targetingVisual = null;
        isTargetingMode = false;
    }

    public void BeginTargetSelectionForCreature(Card creature, Player owner, CardAbility ability)
    {
        targetingCreature = creature;
        targetingAbility = ability;
        isTargetingMode = true;

        bool foundValidTarget = false;

        foreach (var cv in activeCardVisuals)
        {
            if (cv == null || cv.linkedCard == null)
                continue;

            Card target = cv.linkedCard;

            bool correctType =
                (ability.requiredTargetType == SorceryCard.TargetType.Creature && target is CreatureCard) ||
                (ability.requiredTargetType == SorceryCard.TargetType.Artifact && target is ArtifactCard) ||
                (ability.requiredTargetType == SorceryCard.TargetType.Enchantment && target is EnchantmentCard) ||
                (ability.requiredTargetType == SorceryCard.TargetType.Land && target is LandCard);

            bool isOnBattlefield = GetOwnerOfCard(target)?.Battlefield.Contains(target) == true;

            if (correctType && isOnBattlefield)
            {
                foundValidTarget = true;
                break;
            }
        }

        if (!foundValidTarget)
        {
            Debug.Log("No valid targets for creature ETB — skipping ability.");
            targetingCreature = null;
            targetingAbility = null;
            isTargetingMode = false;
        }
        else
        {
            Debug.Log("ETB ability requires target — enter targeting mode.");
        }
    }

    public bool HasValidTargetForAbility(CardAbility ability)
    {
        List<Card> battlefieldCards = new List<Card>();
        battlefieldCards.AddRange(humanPlayer.Battlefield);
        battlefieldCards.AddRange(aiPlayer.Battlefield);

        foreach (Card target in battlefieldCards)
        {
            bool correctType =
                (ability.requiredTargetType == SorceryCard.TargetType.Creature && target is CreatureCard) ||
                (ability.requiredTargetType == SorceryCard.TargetType.Artifact && target is ArtifactCard) ||
                (ability.requiredTargetType == SorceryCard.TargetType.Enchantment && target is EnchantmentCard) ||
                (ability.requiredTargetType == SorceryCard.TargetType.Land && target is LandCard);

            if (correctType)
                return true;
        }

        return false;
    }

    public void BeginOptionalTargetSelectionAfterEntry(Card creature, Player owner, CardAbility ability)
    {
        targetingCreatureOptional = creature;
        optionalAbility = ability;
        optionalTargetPlayer = null;
        pendingStackEffects++;
        isTargetingMode = true;
        targetingVisual = FindCardVisual(creature); // Optional, for visual link

        Debug.Log($"Optional ETB targeting started for {creature.cardName}. Click a valid target if you want to use the ability.");
    }

    public void CancelOptionalTargeting()
    {
        if (targetingCreatureOptional != null)
        {
            Debug.Log("Optional targeting cancelled.");
            targetingCreatureOptional = null;
            optionalAbility = null;
            optionalTargetPlayer = null;
            pendingStackEffects = Mathf.Max(0, pendingStackEffects - 1);
            isTargetingMode = false;
            targetingVisual = null;
        }
    }

    public void ResolveOptionalTargeting(Card target)
    {
        if (targetingCreatureOptional == null || optionalAbility == null)
            return;

        var ability = optionalAbility;
        var source = targetingCreatureOptional;
        Player owner = GetOwnerOfCard(source);

        targetingCreatureOptional = null;
        optionalAbility = null;
        isTargetingMode = false;
        targetingVisual = null;

        QueueTriggeredAbility(ability, owner, source, target);
        pendingStackEffects = Mathf.Max(0, pendingStackEffects - 1);
    }

    public void ResolveOptionalPlayerTargeting(Player target)
    {
        if (targetingCreatureOptional == null || optionalAbility == null)
            return;

        optionalTargetPlayer = target;
        var ability = optionalAbility;
        var source = targetingCreatureOptional;
        Player owner = GetOwnerOfCard(source);

        targetingCreatureOptional = null;
        optionalAbility = null;
        isTargetingMode = false;
        targetingVisual = null;

        QueueTriggeredAbility(ability, owner, source, null);
        pendingStackEffects = Mathf.Max(0, pendingStackEffects - 1);
    }

    public void QueueTriggeredAbility(CardAbility ability, Player owner, Card source, Card target = null, Card deadCreature = null)
    {
        triggerQueue.Enqueue(new TriggeredAbilityContext(ability, owner, source, target, deadCreature));
        pendingStackEffects++;
        if (!processingTriggerQueue)
            StartCoroutine(ProcessTriggerQueue());
    }

    private IEnumerator ProcessTriggerQueue()
    {
        processingTriggerQueue = true;
        while (triggerQueue.Count > 0)
        {
            var ctx = triggerQueue.Dequeue();
            yield return StartCoroutine(ResolveTriggeredAbilityOnStack(ctx.ability, ctx.owner, ctx.source, ctx.target, ctx.deadCreature));
        }
        processingTriggerQueue = false;
    }

    public (int playerDamage, int aiDamage) ResolveCombatForAttacker(CreatureCard attacker)
    {
        int playerDamage = 0;
        int aiDamage = 0;

        // Clamp negative power to zero when dealing damage and respect damage prevention
        int attackerDamage = attacker.keywordAbilities.Contains(KeywordAbility.CantDealCombatDamage)
            ? 0
            : Mathf.Max(attacker.power, 0);

        var blockers = attacker.blockedByThisBlocker;

        if (blockers != null && blockers.Count > 0)
        {
            int remainingDamage = attackerDamage;
            int totalDamageFromBlockers = 0;

            bool attackerHasTrample = attacker.keywordAbilities.Contains(KeywordAbility.Trample);
            for (int i = 0; i < blockers.Count; i++)
            {
                var blocker = blockers[i];
                bool attackerProtected = blocker.color.Any(c => attacker.keywordAbilities.Contains(ProtectionUtils.GetProtectionKeyword(c)));
                bool blockerProtected = attacker.color.Any(c => blocker.keywordAbilities.Contains(ProtectionUtils.GetProtectionKeyword(c)));

                int damageToBlocker = 0;
                if (!blockerProtected)
                {
                    bool isLastBlocker = i == blockers.Count - 1;
                    if (!attackerHasTrample && isLastBlocker)
                        damageToBlocker = remainingDamage;
                    else
                        damageToBlocker = Mathf.Min(remainingDamage, blocker.toughness);

                    blocker.TakeDamage(damageToBlocker);
                    if (attacker.keywordAbilities.Contains(KeywordAbility.Deathtouch) && damageToBlocker > 0)
                        blocker.Kill();
                    remainingDamage -= damageToBlocker;
                }

                int damageFromBlocker = (attackerProtected || blocker.keywordAbilities.Contains(KeywordAbility.CantDealCombatDamage))
                    ? 0
                    : Mathf.Max(blocker.power, 0);
                if (!attackerProtected)
                {
                    totalDamageFromBlockers += damageFromBlocker;
                    if (blocker.keywordAbilities.Contains(KeywordAbility.Deathtouch) && damageFromBlocker > 0)
                        attacker.Kill();
                }

                if (damageToBlocker > 0 || damageFromBlocker > 0)
                    SoundManager.Instance.PlaySound(SoundManager.Instance.impact);

                if (attacker.keywordAbilities.Contains(KeywordAbility.Lifelink) && damageToBlocker > 0)
                {
                    Player owner = GetOwnerOfCard(attacker);
                    TryGainLife(owner, damageToBlocker);
                }

                if (blocker.keywordAbilities.Contains(KeywordAbility.Lifelink) && damageFromBlocker > 0)
                {
                    Player blockerOwner = GetOwnerOfCard(blocker);
                    TryGainLife(blockerOwner, damageFromBlocker);
                }
            }

            attacker.TakeDamage(totalDamageFromBlockers);

            if (attacker.keywordAbilities.Contains(KeywordAbility.Trample) && remainingDamage > 0)
            {
                if (humanPlayer.Battlefield.Contains(attacker))
                {
                    aiPlayer.Life -= remainingDamage;
                    aiDamage += remainingDamage;
                    NotifyCombatDamageToPlayer(attacker, aiPlayer);
                }
                else
                {
                    humanPlayer.Life -= remainingDamage;
                    playerDamage += remainingDamage;
                    NotifyCombatDamageToPlayer(attacker, humanPlayer);
                }

                if (attacker.keywordAbilities.Contains(KeywordAbility.Lifelink))
                {
                    TryGainLife(GetOwnerOfCard(attacker), remainingDamage);
                }
            }
        }
        else
        {
            if (humanPlayer.Battlefield.Contains(attacker))
            {
                aiPlayer.Life -= attackerDamage;
                aiDamage += attackerDamage;
                NotifyCombatDamageToPlayer(attacker, aiPlayer);

                if (attacker.keywordAbilities.Contains(KeywordAbility.Lifelink))
                {
                    Player owner = humanPlayer.Battlefield.Contains(attacker) ? humanPlayer : aiPlayer;
                    TryGainLife(owner, attackerDamage);
                }
            }
            else
            {
                humanPlayer.Life -= attackerDamage;
                playerDamage += attackerDamage;
                NotifyCombatDamageToPlayer(attacker, humanPlayer);

                if (attacker.keywordAbilities.Contains(KeywordAbility.Lifelink))
                {
                    TryGainLife(aiPlayer, attackerDamage);
                }
            }
        }

        return (playerDamage, aiDamage);
    }

    public IEnumerator ResolveCombatWithAnimations()
    {
        VisualEffectManager.Instance.ResetLifeDeltas();
        foreach (var attacker in new List<CreatureCard>(currentAttackers))
        {
            CardVisual attackerVisual = FindCardVisual(attacker);
            if (attackerVisual == null)
            {
                continue;
            }
            Vector3 startPos = attackerVisual.transform.position;

            if (attacker.blockedByThisBlocker != null && attacker.blockedByThisBlocker.Count > 0)
            {
                var blockerVisual = FindCardVisual(attacker.blockedByThisBlocker[0]);
                if (blockerVisual != null)
                {
                    yield return StartCoroutine(VisualEffectManager.Instance.MoveCard(attackerVisual.transform, startPos, blockerVisual.transform.position, 0.15f));
                }
            }
            else
            {
                yield return StartCoroutine(VisualEffectManager.Instance.MoveCardToLifeContainer(attackerVisual.transform, startPos, !humanPlayer.Battlefield.Contains(attacker), 0.15f));
            }
            yield return new WaitForSeconds(0.05f);

            var (pd, ad) = ResolveCombatForAttacker(attacker);
            if (pd > 0)
                VisualEffectManager.Instance.ShowFloatingDamageForPlayer(pd, true);
            if (ad > 0)
                VisualEffectManager.Instance.ShowFloatingDamageForPlayer(ad, false);

            if (activeCardVisuals.Contains(attackerVisual))
            {
                yield return StartCoroutine(VisualEffectManager.Instance.MoveCard(attackerVisual.transform, attackerVisual.transform.position, startPos, 0.15f));
            }

            yield return new WaitForSeconds(0.05f);
        }

        foreach (var creature in humanPlayer.Battlefield.Concat(aiPlayer.Battlefield).OfType<CreatureCard>())
        {
            if (creature.cardName == "Undead Army" &&
                (currentAttackers.Contains(creature) || creature.blockingThisAttacker != null))
            {
                creature.AddMinusOneCounter();
                var vis = FindCardVisual(creature);
                if (vis != null) vis.UpdateVisual();
            }
        }

        CheckDeaths(humanPlayer);
        CheckDeaths(aiPlayer);

        foreach (var card in humanPlayer.Battlefield)
        {
            if (card is CreatureCard c)
            {
                c.blockingThisAttacker = null;
                c.blockedByThisBlocker.Clear();
            }
        }
        foreach (var card in aiPlayer.Battlefield)
        {
            if (card is CreatureCard c)
            {
                c.blockingThisAttacker = null;
                c.blockedByThisBlocker.Clear();
            }
        }

        currentAttackers.Clear();
        selectedBlockerForBlocking = null;
        UpdateUI();
        VisualEffectManager.Instance.FinalizeLifeDeltas();
    }

    private IEnumerator ShowDeathVFXAndDelayLayout(Card card, Player owner, CardVisual visual, GameObject overridePrefab = null)
    {
        if (visual != null)
            visual.EnableTargetingHighlight(false); // ensure highlight removed

        pendingGraveyardAnimations++;

        // 1. Create a placeholder object in the same layout slot
        GameObject placeholderPrefab = overridePrefab != null ? overridePrefab : deathPlaceholderPrefab;
        GameObject placeholder = Instantiate(placeholderPrefab, visual.transform.parent);
        placeholder.transform.SetSiblingIndex(visual.transform.GetSiblingIndex());
        placeholder.transform.localScale = visual.transform.localScale;
        placeholder.transform.localPosition = visual.transform.localPosition + placeholder.transform.localPosition;

        // 2. Remove the visual instantly, preserving layout slot
        activeCardVisuals.Remove(visual);
        Destroy(visual.gameObject);

        // 3. Wait for VFX duration
        yield return new WaitForSeconds(1.5f); // Match blood splat prefab's lifespan

        // 4. Now destroy the placeholder — this causes the layout to update
        Destroy(placeholder);

        // 5. Create graveyard visual (if not a token)
        if (!card.isToken)
        {
            GameObject visualGO = Instantiate(cardPrefab,
                owner == humanPlayer ? playerGraveyardArea : aiGraveyardArea);
            CardVisual graveyardVisual = visualGO.GetComponent<CardVisual>();
            graveyardVisual.Setup(card, this);
            graveyardVisual.transform.SetParent(owner == humanPlayer ? playerGraveyardArea : aiGraveyardArea);
            graveyardVisual.transform.localPosition = Vector3.zero;
            graveyardVisual.UpdateGraveyardVisual();
            // Ensure overlay elements appear above
            // Place newest card on top of pile
            graveyardVisual.transform.SetAsLastSibling();
            EnsureGraveyardCounterOnTop(owner);

            activeCardVisuals.Add(graveyardVisual);
        }

        // 6. Move to graveyard data list (skip for tokens)
        if (!card.isToken)
            owner.Graveyard.Add(card);
        UpdateUI();
        pendingGraveyardAnimations--;
    }

    private IEnumerator ShowHandDiscardVFX(Card card, Player owner, CardVisual visual)
    {
        if (visual != null)
            visual.EnableTargetingHighlight(false); // ensure highlight removed

        // 1. Create placeholder
        Transform parent = visual.transform.parent;
        GameObject placeholder = Instantiate(deathPlaceholderPrefab, parent);
        placeholder.transform.SetSiblingIndex(visual.transform.GetSiblingIndex());
        placeholder.transform.localScale = visual.transform.localScale;
        placeholder.transform.localPosition = visual.transform.localPosition;

        // 2. Remove the visual (like battlefield death)
        GameManager.Instance.activeCardVisuals.Remove(visual);
        Destroy(visual.gameObject);

        // 3. Wait for VFX duration (even shorter than battlefield)
        yield return new WaitForSeconds(0.5f);

        // 4. Destroy placeholder to allow layout rebuild
        Destroy(placeholder);

        // 5. Create graveyard visual
        if (!card.isToken)
        {
            GameObject visualGO = Instantiate(GameManager.Instance.cardPrefab,
                owner == GameManager.Instance.humanPlayer
                    ? GameManager.Instance.playerGraveyardArea
                    : GameManager.Instance.aiGraveyardArea);
            CardVisual graveyardVisual = visualGO.GetComponent<CardVisual>();
            graveyardVisual.Setup(card, GameManager.Instance);
            graveyardVisual.transform.localPosition = Vector3.zero;
            graveyardVisual.UpdateGraveyardVisual();
            // Keep UI overlay elements on top
            // Maintain newest card on top of pile
            graveyardVisual.transform.SetAsLastSibling();
            EnsureGraveyardCounterOnTop(owner);

            GameManager.Instance.activeCardVisuals.Add(graveyardVisual);
        }

        // 6. Move to graveyard data list (skip for tokens)
        if (!card.isToken)
            owner.Graveyard.Add(card);
        UpdateUI();
    }

    public void NotifyArtifactEntered(Card artifact, Player controller)
    {
        lastEnteredArtifact = artifact;
        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnArtifactEnter && ability.effect != null)
                    {
                        if (card == artifact)
                            continue;

                        int oldLife = player.Life;
                        ability.effect.Invoke(player, card);
                        int gained = player.Life - oldLife;
                        if (gained > 0)
                        {
                            VisualEffectManager.Instance.ShowFloatingHealForPlayer(gained, player == humanPlayer);
                        }
                    }
                }
            }
        }
        lastEnteredArtifact = null;
    }

    public void NotifyEnchantmentEntered(Card enchantment, Player controller)
    {
        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnEnchantmentEnter && ability.effect != null)
                    {
                        if (card == enchantment)
                            continue;

                        int oldLife = player.Life;
                        ability.effect.Invoke(player, card);
                        int gained = player.Life - oldLife;
                        if (gained > 0)
                        {
                            VisualEffectManager.Instance.ShowFloatingHealForPlayer(gained, player == humanPlayer);
                        }
                    }
                }
            }
        }
    }

    public void NotifyLandEntered(Card land, Player controller)
    {
        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnLandEnter && ability.effect != null)
                    {
                        int oldLife = player.Life;
                        ability.effect.Invoke(player, card);
                        int gained = player.Life - oldLife;
                        if (gained > 0)
                        {
                            VisualEffectManager.Instance.ShowFloatingHealForPlayer(gained, player == humanPlayer);
                        }
                    }
                }
            }
        }
    }

    public void NotifyCreatureEntered(Card creature, Player controller)
    {
        if (!(creature is CreatureCard))
            return;

        lastEnteredCreature = creature;
        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnCreatureEnter && ability.effect != null)
                    {
                        ability.effect.Invoke(player, card);
                    }
                }
            }
        }
        lastEnteredCreature = null;
    }

    public void NotifyLandLeft(Card land, Player controller)
    {
        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnLandLeave && ability.effect != null)
                    {
                        int oldLife = player.Life;
                        ability.effect.Invoke(player, card);
                        int gained = player.Life - oldLife;
                        if (gained > 0)
                        {
                            VisualEffectManager.Instance.ShowFloatingHealForPlayer(gained, player == humanPlayer);
                        }
                    }
                }
            }
        }
    }

    public int lastLifeGainedAmount = 0;

    public void NotifyLifeGain(Player player, int amount)
    {
        lastLifeGainedAmount = amount;
        foreach (var card in player.Battlefield.ToList())
        {
            foreach (var ability in card.abilities)
            {
                if (ability.timing == TriggerTiming.OnLifeGain && ability.effect != null)
                {
                    int oldLife = player.Life;
                    ability.effect.Invoke(player, card);
                    int gained = player.Life - oldLife;
                    if (gained > 0)
                    {
                        VisualEffectManager.Instance.ShowFloatingHealForPlayer(gained, player == humanPlayer);
                    }
                }
            }
        }
        lastLifeGainedAmount = 0;
    }

    public int lastCardsDrawnAmount = 0;

    public Player lastDiscardingPlayer = null;

    public Card lastDeadCreature = null;

    public Card lastEnteredCreature = null;

    public Card lastEnteredArtifact = null;

    // Tracks the source card of the ability currently being resolved.
    public Card lastAbilitySource = null;

    public void NotifyCardDrawn(Player player, int amount)
    {
        lastCardsDrawnAmount = amount;
        foreach (var card in player.Battlefield.ToList())
        {
            foreach (var ability in card.abilities)
            {
                if (ability.timing == TriggerTiming.OnCardDraw && ability.effect != null)
                {
                    ability.effect.Invoke(player, card);
                }
            }
        }
        lastCardsDrawnAmount = 0;
    }

    public void NotifyOpponentDraw(Player drawingPlayer)
    {
        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            if (player == drawingPlayer)
                continue;

            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnOpponentDraw && ability.effect != null)
                    {
                        ability.effect.Invoke(player, card);
                    }
                }
            }
        }
    }

    public void NotifyOpponentDiscard(Player discardingPlayer)
    {
        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            if (player == discardingPlayer)
                continue;

            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnOpponentDiscard && ability.effect != null)
                    {
                        ability.effect.Invoke(player, card);
                    }
                }
            }
        }
    }

    public void NotifyPlayerDiscard(Player discardingPlayer)
    {
        lastDiscardingPlayer = discardingPlayer;
        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnPlayerDiscard && ability.effect != null)
                    {
                        ability.effect.Invoke(player, card);
                    }
                }
            }
        }
        lastDiscardingPlayer = null;
    }

    public void NotifyCreatureDiesOrDiscarded(Card creature, Player owner)
    {
        if (!(creature is CreatureCard))
            return;

        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnCreatureDiesOrDiscarded && ability.effect != null)
                    {
                        QueueTriggeredAbility(ability, player, card, card, creature);
                    }
                }
            }
        }
    }

    public void NotifyCreatureDies(Card creature, Player owner)
    {
        if (!(creature is CreatureCard))
            return;

        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnCreatureDies && ability.effect != null)
                    {
                        QueueTriggeredAbility(ability, player, card, card, creature);
                    }
                }
            }
        }
    }

    public void NotifyCombatDamageToPlayer(CreatureCard attacker, Player target)
    {
        foreach (var player in new[] { humanPlayer, aiPlayer })
        {
            foreach (var card in player.Battlefield.ToList())
            {
                foreach (var ability in card.abilities)
                {
                    if (ability.timing == TriggerTiming.OnCombatDamageToPlayer && ability.effect != null)
                    {
                        ability.effect.Invoke(player, card);
                    }
                }
            }
        }
    }

    public void NotifyCreatureBlocks(CreatureCard blocker, CreatureCard attacker)
    {
        if (blocker == null || attacker == null)
            return;

        Player owner = GetOwnerOfCard(blocker);
        if (owner == null)
            return;

        foreach (var ability in blocker.abilities)
        {
            if (ability.timing == TriggerTiming.OnBlock && ability.effect != null)
            {
                QueueTriggeredAbility(ability, owner, blocker, attacker);
            }
        }
    }

    public void GainLife(Player player, int amount)
    {
        TryGainLife(player, amount);
    }

    private void AwardFavouriteCardCoins(Card card, Player caster)
    {
        if (caster == humanPlayer && !string.IsNullOrEmpty(favouriteCardName) && card.cardName == favouriteCardName)
        {
            CoinsManager.AddCoins(5);
            VisualEffectManager.Instance.ShowFavouritePopup();
        }
    }

    public void CheckForGameEnd()
    {
        if (gameOver)
            return;

        if (aiPlayer.Life <= 0 && humanPlayer.Life <= 0)
        {
            Debug.Log("Both players died — draw counts as a loss for the human player.");
            gameOver = true;
            if (TurnSystem.Instance != null)
                TurnSystem.Instance.StopAllCoroutines();
            FindObjectOfType<WinScreenUI>().ShowLoseScreen();
        }
        else if (aiPlayer.Life <= 0)
        {
            Debug.Log("AI defeated — player wins!");
            CardData reward = PlayerCollection.AddRandomCard();
            gameOver = true;
            if (TurnSystem.Instance != null)
                TurnSystem.Instance.StopAllCoroutines();
            FindObjectOfType<WinScreenUI>().ShowWinScreen(reward);
        }
        else if (humanPlayer.Life <= 0)
        {
            Debug.Log("Human player defeated — game lost.");
            gameOver = true;
            if (TurnSystem.Instance != null)
                TurnSystem.Instance.StopAllCoroutines();
            FindObjectOfType<WinScreenUI>().ShowLoseScreen();
        }
    }
}
