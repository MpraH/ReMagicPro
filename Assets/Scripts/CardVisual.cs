using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class CardVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Card linkedCard; // which card it represents
    public GameManager gameManager; // which manager it talks to
    public CreatureCard blockingThisAttacker; // If blocking, who am I blocking
    public List<CreatureCard> blockedByThisBlocker = new List<CreatureCard>(); // If attacker, who blocks me
    public LineRenderer lineRenderer;
    public Image artImage;
    public Image backgroundImage;
    public Image cardRarity;
    public Image coloredManaIcon1;
    public Image coloredManaIcon2;

    public Sprite landBorder;
    public Sprite whiteBorder;
    public Sprite blueBorder;
    public Sprite blackBorder;
    public Sprite redBorder;
    public Sprite greenBorder;
    public Sprite artifactBorder;
    public Sprite defaultBorder;
    public Sprite multicolorBorder;
    public Sprite whiteManaSymbol;
    public Sprite blueManaSymbol;
    public Sprite blackManaSymbol;
    public Sprite redManaSymbol;
    public Sprite greenManaSymbol;
    public Sprite whiteLandIcon;
    public Sprite blueLandIcon;
    public Sprite blackLandIcon;
    public Sprite redLandIcon;
    public Sprite greenLandIcon;

    public GameObject costBackground;
    public GameObject statsBackground;
    public GameObject swordIcon;
    public GameObject shieldIcon;
    public GameObject tapIcon;
    public GameObject genericCostBG;
    public GameObject landIcon;
    public GameObject highlightBorder;

    public TMP_Text titleText;
    public TMP_Text sicknessText;
    public TMP_Text costText;
    public TMP_Text statsText;
    public TMP_Text keywordText;
    public TMP_Text cardTypeText;
    public TMP_Text artistText;

    // UI elements showing +1/+1 or -1/-1 counters
    public GameObject counterContainer;
    public TMP_Text counterText;
    public Image counterImage;

    private readonly Vector2 battlefieldStatsPosition = new Vector2(0, -5);
    private readonly Vector2 defaultStatsPosition = new Vector2(28, -53);
    private Vector3 originalPosition;

    // When true, hovering the card will not move it or show a preview
    public bool disableHoverEffects = false;
    public bool isInBattlefield = false;
    public bool isInGraveyard = false;
    public bool isInGraveyardViewer = false; // true when shown in the Graveyard UI
    public bool isInStack = false; // when true, card is on the stack

    private bool isPointerOver = false;
    private bool lastBattlefieldState = false;
    private List<Graphic> raycastGraphics = new List<Graphic>();
    private readonly Dictionary<Graphic, bool> originalRaycastStates = new Dictionary<Graphic, bool>();

    void Awake()
    {
        DisableRaycast(highlightBorder);

        if (artImage == null)
        {
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                string name = img.gameObject.name.ToLower();
                if (name == "artimage" || name == "artwork" || name == "art")
                {
                    artImage = img;
                    break;
                }
            }

            if (artImage == null)
                artImage = GetComponentInChildren<Image>(true);
        }

        if (artImage != null && artImage.sprite != null)
        {
            var tex = artImage.sprite.texture;
            if (tex != null && tex.isReadable)
            {
                artImage.alphaHitTestMinimumThreshold = 0.1f;
            }
            else
            {
                Debug.LogWarning($"Texture for {artImage.name} is not readable; alpha hit testing disabled.");
            }
        }

        DisableRaycast(cardRarity);
        DisableRaycast(coloredManaIcon1);
        DisableRaycast(coloredManaIcon2);
        DisableRaycast(costBackground);
        DisableRaycast(statsBackground);
        DisableRaycast(swordIcon);
        DisableRaycast(shieldIcon);
        DisableRaycast(tapIcon);
        DisableRaycast(genericCostBG);
        DisableRaycast(landIcon);
        DisableRaycast(counterImage);

        raycastGraphics = GetComponentsInChildren<Graphic>(true).ToList();
        foreach (var g in raycastGraphics)
            originalRaycastStates[g] = g.raycastTarget;

        UpdateRaycastTargets();
    }

    private void DisableRaycast(Image img)
    {
        if (img != null)
            img.raycastTarget = false;
    }

    private void DisableRaycast(GameObject obj)
    {
        if (obj == null)
            return;
        var img = obj.GetComponent<Image>();
        if (img != null)
            img.raycastTarget = false;
    }

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "DeckEditorScene")
            return;
        GetComponent<Button>().onClick.AddListener(OnClick);

        UpdateRaycastTargets();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (disableHoverEffects)
            return;

        if ((isInGraveyard && !isInGraveyardViewer) || isInStack || linkedCard == null || linkedCard.artwork == null)
            return;

        CardHoverPreview.Instance.ShowCard(linkedCard);

        string scene = SceneManager.GetActiveScene().name;
        if (scene == "DeckBuilderScene" || scene == "DeckEditorScene")
        {
            if (highlightBorder != null)
                highlightBorder.SetActive(true);
            return;
        }

        if (!isInBattlefield && !isInGraveyard) // assume this means it's in hand
        {
            originalPosition = transform.localPosition;
            transform.localPosition += Vector3.up * 30f;
        }
        else if (!isInGraveyardViewer)
        {
            transform.localScale = Vector3.one * 1.1f;
        }

        isPointerOver = true;
        UpdateConnectionLine();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (disableHoverEffects)
            return;

        if ((isInGraveyard && !isInGraveyardViewer) || isInStack)
            return;

        CardHoverPreview.Instance.HidePreview();

        string scene = SceneManager.GetActiveScene().name;
        if (scene == "DeckBuilderScene" || scene == "DeckEditorScene")
        {
            if (highlightBorder != null)
                highlightBorder.SetActive(false);
            return;
        }

        if (!isInBattlefield && !isInGraveyard) // in hand
        {
            transform.localPosition = originalPosition;
        }
        else if (!isInGraveyardViewer)
        {
            transform.localScale = Vector3.one;
        }

        isPointerOver = false;

        if (lineRenderer != null &&
            !(linkedCard is CreatureCard creature && creature.blockingThisAttacker != null) &&
            !IsUnblockedAttacker())
        {
            lineRenderer.enabled = false;
        }
    }

    private void UpdateLandIcon()
    {
        if (landIcon == null)
            return;

        if (linkedCard is LandCard && !isInBattlefield)
        {
            Sprite landSprite = linkedCard.PrimaryColor switch
            {
                "White" => whiteLandIcon,
                "Blue" => blueLandIcon,
                "Black" => blackLandIcon,
                "Red" => redLandIcon,
                "Green" => greenLandIcon,
                _ => null
            };

            landIcon.GetComponent<Image>().sprite = landSprite;
            landIcon.SetActive(landSprite != null);
        }
        else
        {
            landIcon.SetActive(false);
        }
    }

    private void SetTypeLine(CardData data)
    {
        if (cardTypeText == null || data == null) return;

        string typeLine;

        if (data.cardType == CardType.Creature)
        {
            bool isArtifactCreature = data.color != null && data.color.Contains("Artifact");
            string baseType = isArtifactCreature ? "Artifact Creature" : "Creature";

            if (data.subtypes != null && data.subtypes.Count > 0)
                typeLine = $"{baseType} — {string.Join(" ", data.subtypes)}";
            else
                typeLine = baseType;
        }
        else if (data.cardType == CardType.Enchantment && data.subtypes != null && data.subtypes.Count > 0)
        {
            typeLine = $"Enchantment — {string.Join(" ", data.subtypes)}";
        }
        else if (data.cardType == CardType.Artifact && data.subtypes != null && data.subtypes.Count > 0)
        {
            typeLine = $"Artifact — {string.Join(" ", data.subtypes)}";
        }
        else
        {
            typeLine = data.cardType.ToString();
        }

        cardTypeText.text = typeLine;
        cardTypeText.enabled = !isInBattlefield;
    }

    private int CalculateGenericCost()
    {
        if (linkedCard is CreatureCard c)
        {
            return (linkedCard.PrimaryColor == "Artifact" || linkedCard.PrimaryColor == "None")
                ? c.manaCost
                : Mathf.Max(c.manaCost - linkedCard.color.Count, 0);
        }
        if (linkedCard is SorceryCard s)
        {
            return Mathf.Max(s.manaCost - linkedCard.color.Count, 0);
        }
        if (linkedCard is ArtifactCard a)
        {
            return a.manaCost;
        }
        if (linkedCard is EnchantmentCard e)
        {
            return Mathf.Max(e.manaCost - linkedCard.color.Count, 0);
        }
        return 0;
    }

    private string GetCostDisplay(int genericCost)
    {
        if (linkedCard.hasXCost)
        {
            string basePart = genericCost > 0 ? genericCost.ToString() : "";
            string xPart = (linkedCard.xValue > 0 && (isInBattlefield || isInStack))
                ? linkedCard.xValue.ToString()
                : "X";
            return string.IsNullOrEmpty(basePart) ? xPart : basePart + "+" + xPart;
        }
        if (genericCost == 0)
        {
            if (linkedCard is ArtifactCard)
            {
                return "0";
            }
            if (linkedCard is CreatureCard && linkedCard.PrimaryColor == "Artifact")
            {
                return "0";
            }
        }
        return genericCost > 0 ? genericCost.ToString() : "";
    }

    private string ColorStat(int current, int baseValue)
    {
        if (current > baseValue)
            // Use a darker shade of green (emerald) when stats are boosted
            return $"<color=#50C878>{current}</color>";
        if (current < baseValue)
            return $"<color=#ff0000>{current}</color>";
        return current.ToString();
    }

    private string FormatStats(CreatureCard creature)
    {
        return $"{ColorStat(creature.power, creature.basePower)}/{ColorStat(creature.toughness, creature.baseToughness)}";
    }

    private bool IsAttacking()
    {
        return gameManager != null &&
               linkedCard is CreatureCard creature &&
               gameManager.currentAttackers.Contains(creature);
    }

    private bool IsUnblockedAttacker()
    {
        return IsAttacking() && ((CreatureCard)linkedCard).blockedByThisBlocker.Count == 0;
    }

    private void UpdateConnectionLine()
    {
        if (lineRenderer == null)
            return;

        if (gameManager == null)
        {
            lineRenderer.enabled = false;
            return;
        }

        if (IsAttacking())
        {
            var attackingCreature = (CreatureCard)linkedCard;
            if (attackingCreature.blockedByThisBlocker.Count == 0)
            {
                Transform target = VisualEffectManager.Instance.GetLifeContainer(!gameManager.humanPlayer.Battlefield.Contains(attackingCreature)).transform;

                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, new Vector3(transform.position.x, transform.position.y, 0));
                lineRenderer.SetPosition(1, new Vector3(target.position.x, target.position.y, 0));
            }
            else
            {
                lineRenderer.enabled = false;
            }
        }
        else if (linkedCard is CreatureCard creature && creature.blockingThisAttacker != null)
        {
            var attackerVisual = gameManager.FindCardVisual(creature.blockingThisAttacker);
            if (attackerVisual != null)
            {
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, new Vector3(transform.position.x, transform.position.y, 0));
                lineRenderer.SetPosition(1, new Vector3(attackerVisual.transform.position.x, attackerVisual.transform.position.y, 0));
            }
            else
            {
                lineRenderer.enabled = false;
            }
        }
        else if (isPointerOver && isInBattlefield && linkedCard is AuraCard aura && aura.attachedTo != null)
        {
            var targetVisual = gameManager.FindCardVisual(aura.attachedTo);
            if (targetVisual != null)
            {
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, new Vector3(transform.position.x, transform.position.y, 0));
                lineRenderer.SetPosition(1, new Vector3(targetVisual.transform.position.x, targetVisual.transform.position.y, 0));
            }
            else
            {
                lineRenderer.enabled = false;
            }
        }
        else if (isPointerOver && isInBattlefield && linkedCard is EquipmentCard equip && equip.equippedTo != null)
        {
            var targetVisual = gameManager.FindCardVisual(equip.equippedTo);
            if (targetVisual != null)
            {
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, new Vector3(transform.position.x, transform.position.y, 0));
                lineRenderer.SetPosition(1, new Vector3(targetVisual.transform.position.x, targetVisual.transform.position.y, 0));
            }
            else
            {
                lineRenderer.enabled = false;
            }
        }
        else
        {
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        if (lastBattlefieldState != isInBattlefield)
        {
            lastBattlefieldState = isInBattlefield;
            UpdateRaycastTargets();
        }

        if (lineRenderer == null || gameManager == null)
            return;

        bool isBlocking = linkedCard is CreatureCard creature && creature.blockingThisAttacker != null;
        bool isAttacking = IsAttacking();

        if (isPointerOver || isBlocking || isAttacking)
        {
            UpdateConnectionLine();
        }
    }

    private void UpdateRaycastTargets()
    {
        foreach (var g in raycastGraphics)
        {
            if (g == null)
                continue;

            if (isInBattlefield)
            {
                g.raycastTarget = false;
            }
            else if (originalRaycastStates.TryGetValue(g, out var original))
            {
                g.raycastTarget = original;
            }
        }

        if (isInBattlefield && artImage != null)
            artImage.raycastTarget = true;
    }

    public void UpdateVisual()
    {
        var data = CardDatabase.GetCardData(linkedCard.cardName);
        SetCardBorder(data);
        UpdateLandIcon();

        if (artistText != null)
        {
            if (data != null && !string.IsNullOrEmpty(data.artist) && !isInBattlefield)
            {
                artistText.text = data.artist;
                artistText.gameObject.SetActive(true);
            }
            else
            {
                artistText.gameObject.SetActive(false);
            }
        }

        // Display +1/+1 or -1/-1 counters if present
        if (counterContainer != null)
        {
            if (linkedCard is CreatureCard contersCreature)
            {
                int plus = contersCreature.plusOneCounters;
                int minus = contersCreature.minusOneCounters;

                if (plus == 0 && minus == 0)
                {
                    counterContainer.SetActive(false);
                }
                else
                {
                    counterContainer.SetActive(true);
                    bool showPlus = plus >= minus;

                    if (counterText != null)
                        counterText.text = showPlus ? plus.ToString() : minus.ToString();

                    if (counterImage != null)
                    {
                        counterImage.color = showPlus
                            ? new Color(0.6f, 0.9f, 0.6f, 1f)
                            : new Color(0.8f, 0.6f, 0.9f, 1f);
                    }
                }
            }
            else if (linkedCard is EnchantmentCard enchantment)
            {
                int minus = enchantment.minusOneCounters;
                if (minus == 0)
                {
                    counterContainer.SetActive(false);
                }
                else
                {
                    counterContainer.SetActive(true);
                    if (counterText != null)
                        counterText.text = minus.ToString();
                    if (counterImage != null)
                        counterImage.color = new Color(0.8f, 0.6f, 0.9f, 1f);
                }
            }
            else
            {
                counterContainer.SetActive(false);
            }
        }

        if (cardRarity != null)
        {
            if (data != null && data.rarity != "Token")
            {
                cardRarity.color = GetRarityColor(data.rarity);
                cardRarity.enabled = true;
            }
            else
            {
                cardRarity.enabled = false;
            }
        }

        SetTypeLine(data);

        transform.rotation = linkedCard.isTapped
            ? Quaternion.Euler(0, 0, -30)
            : Quaternion.identity;

        if (tapIcon != null)
            tapIcon.SetActive(linkedCard.isTapped);

        if (swordIcon != null && linkedCard is CreatureCard)
        {
            bool showSword = false;

            if (GameManager.Instance != null &&
                GameManager.Instance.currentAttackers != null &&
                GameManager.Instance.selectedAttackers != null)
            {
                showSword =
                    GameManager.Instance.currentAttackers.Contains(linkedCard) ||
                    GameManager.Instance.selectedAttackers.Contains(linkedCard);
            }

            swordIcon.SetActive(showSword);
            swordIcon.transform.rotation = Quaternion.identity;
        }

        if (shieldIcon != null && linkedCard is CreatureCard)
        {
            CreatureCard cc = (CreatureCard)linkedCard;

            bool showShield = cc.blockingThisAttacker != null && isInBattlefield;

            shieldIcon.SetActive(showShield);
            shieldIcon.transform.rotation = Quaternion.identity;
        }

        if (isInBattlefield)
        {
            if (backgroundImage != null) backgroundImage.enabled = false;
            costBackground.SetActive(false);
            if (cardRarity != null) cardRarity.enabled = false;
            titleText.text = "";
            costText.text = "";
            sicknessText.text = "";
            keywordText.text = "";

            if (linkedCard is CreatureCard battlefieldCreature)
            {
                statsText.text = FormatStats(battlefieldCreature);

                if (isInBattlefield)
                {
                    sicknessText.text = battlefieldCreature.hasSummoningSickness ? "(@)" : "";
                }
                else
                {
                    sicknessText.text = "";
                }

                statsBackground.SetActive(true);
                RectTransform statsRect = statsBackground.GetComponent<RectTransform>();
                if (statsRect != null)
                    statsRect.anchoredPosition = battlefieldStatsPosition;
            }

            UpdateConnectionLine();
            return;
        }

        costText.text = "";
        statsText.text = "";
        keywordText.text = "";
        sicknessText.text = "";

        int genericCost = CalculateGenericCost();

        bool isOneColoredMana = (linkedCard.manaCost == 1) &&
                                linkedCard.PrimaryColor != "Artifact" &&
                                linkedCard.PrimaryColor != "None";

        if (isOneColoredMana)
        {
            if (genericCostBG != null) genericCostBG.SetActive(false);
        }
        else
        {
            bool showGeneric = genericCost > 0 ||
                               linkedCard.PrimaryColor == "Artifact" ||
                               linkedCard.PrimaryColor == "None" ||
                               linkedCard.hasXCost;

            costText.text = showGeneric ? GetCostDisplay(genericCost) : "";
            if (genericCostBG != null) genericCostBG.SetActive(showGeneric);
        }

        if (linkedCard is CreatureCard creature)
        {
            bool showGeneric = genericCost > 0 ||
                               linkedCard.PrimaryColor == "Artifact" ||
                               linkedCard.PrimaryColor == "None" ||
                               linkedCard.hasXCost;

            costText.text = showGeneric ? GetCostDisplay(genericCost) : "";
            if (genericCostBG != null) genericCostBG.SetActive(showGeneric);

            statsText.text = FormatStats(creature);
            keywordText.text = linkedCard.GetCardText();

            if (isInBattlefield)
            {
                sicknessText.text = creature.hasSummoningSickness ? "(@)" : "";
            }
            else
            {
                sicknessText.text = "";
            }


            costBackground.SetActive(true);
            statsBackground.SetActive(true);
        }

        else if (linkedCard is SorceryCard sorcery)
        {
            bool showGeneric = genericCost > 0 || linkedCard.hasXCost;
            costText.text = showGeneric ? GetCostDisplay(genericCost) : "";
            if (genericCostBG != null) genericCostBG.SetActive(showGeneric);

            sorceryEffect(sorcery);

            costBackground.SetActive(true);
            statsBackground.SetActive(false);
        }
        else if (linkedCard is ArtifactCard artifact)
        {
            costText.text = GetCostDisplay(genericCost);
            if (genericCostBG != null) genericCostBG.SetActive(true);
            keywordText.text = linkedCard.GetCardText();

            costBackground.SetActive(true);
            statsBackground.SetActive(false);
        }
        else if (linkedCard is EnchantmentCard enchantment)
        {
            bool showGeneric = genericCost > 0 || linkedCard.hasXCost;
            costText.text = showGeneric ? GetCostDisplay(genericCost) : "";
            if (genericCostBG != null) genericCostBG.SetActive(showGeneric);
            keywordText.text = linkedCard.GetCardText();

            costBackground.SetActive(true);
            statsBackground.SetActive(false);
        }
        else if (linkedCard is LandCard)
        {
            costText.text = "";
            statsText.text = "";
            keywordText.text = "";

            costBackground.SetActive(false);
            statsBackground.SetActive(false);
        }
        else
        {
            costText.text = "";
            statsText.text = "";
            keywordText.text = "";

            costBackground.SetActive(false);
            statsBackground.SetActive(false);
        }

        coloredManaIcon1.gameObject.SetActive(false);
        coloredManaIcon2.gameObject.SetActive(false);

        if (linkedCard.color != null)
        {
            for (int i = 0; i < linkedCard.color.Count && i < 2; i++)
            {
                Sprite icon = GetIconForColor(linkedCard.color[i]);

                if (i == 0 && icon != null)
                {
                    coloredManaIcon1.sprite = icon;
                    coloredManaIcon1.gameObject.SetActive(true);
                    coloredManaIcon1.enabled = true;
                }
                else if (i == 1 && icon != null)
                {
                    coloredManaIcon2.sprite = icon;
                    coloredManaIcon2.gameObject.SetActive(true);
                    coloredManaIcon2.enabled = true;
                }
            }
        }
    }

    public void Setup(Card card, GameManager manager, CardData sourceData = null)
    {
        linkedCard = card;
        gameManager = manager;
        isInStack = false;

        UpdateLandIcon();

        // Re-enable everything when card is set up (e.g., for hand, stack, etc.)
        if (backgroundImage != null) backgroundImage.enabled = true;
        if (titleText != null) titleText.enabled = true;
        if (sicknessText != null) sicknessText.enabled = true;
        if (costText != null) costText.enabled = true;
        if (statsText != null) statsText.enabled = true;
        if (keywordText != null) keywordText.enabled = true;
        if (artistText != null) artistText.enabled = true;

        var data = CardDatabase.GetCardData(linkedCard.cardName);
        SetTypeLine(data);

        titleText.text = card.cardName;
        lineRenderer = GetComponent<LineRenderer>();
        artImage.sprite = linkedCard.artwork;

        if (cardRarity != null)
        {
            if (data != null && data.rarity != "Token") // Don't show for tokens
            {
                cardRarity.color = GetRarityColor(data.rarity);
                cardRarity.enabled = true;
            }
            else
            {
                cardRarity.enabled = false;
            }
        }

        SetCardBorder(data);

        sicknessText.text = ""; // Clear at start

        int genericCost = CalculateGenericCost();

        bool isOneColoredMana = (linkedCard.manaCost == 1) &&
                                linkedCard.PrimaryColor != "Artifact" &&
                                linkedCard.PrimaryColor != "None";

        if (isOneColoredMana)
        {
            if (genericCostBG != null) genericCostBG.SetActive(false);
        }
        else
        {
            bool showGeneric = genericCost > 0 ||
                               linkedCard.PrimaryColor == "Artifact" ||
                               linkedCard.PrimaryColor == "None" ||
                               linkedCard.hasXCost;

            costText.text = showGeneric ? GetCostDisplay(genericCost) : "";
            if (genericCostBG != null) genericCostBG.SetActive(showGeneric);
        }

        if (linkedCard is CreatureCard creature)
        {
            bool showGeneric = genericCost > 0 ||
                               linkedCard.PrimaryColor == "Artifact" ||
                               linkedCard.PrimaryColor == "None";

            costText.text = showGeneric ? genericCost.ToString() : "";
            if (genericCostBG != null) genericCostBG.SetActive(showGeneric);

            statsText.text = FormatStats(creature);
            keywordText.text = linkedCard.GetCardText();

            costBackground.SetActive(true);
            statsBackground.SetActive(true);
        }
        else if (linkedCard is SorceryCard sorcery)
        {
            bool showGeneric = genericCost > 0 || linkedCard.hasXCost;
            costText.text = showGeneric ? GetCostDisplay(genericCost) : "";
            if (genericCostBG != null) genericCostBG.SetActive(showGeneric);

            statsText.text = "";
            sicknessText.text = "";

            sorceryEffect(sorcery);

            costBackground.SetActive(true);
            statsBackground.SetActive(false);
        }
        else if (linkedCard is ArtifactCard artifact)
        {
            costText.text = GetCostDisplay(genericCost);
            if (genericCostBG != null) genericCostBG.SetActive(true);
            statsText.text = "";
            keywordText.text = artifact.GetCardText();

            costBackground.SetActive(true);
            statsBackground.SetActive(false);
        }
        else if (linkedCard is EnchantmentCard enchantment)
        {
            bool showGeneric = genericCost > 0 || linkedCard.hasXCost;
            costText.text = showGeneric ? GetCostDisplay(genericCost) : "";
            if (genericCostBG != null) genericCostBG.SetActive(showGeneric);
            keywordText.text = linkedCard.GetCardText();

            costBackground.SetActive(true);
            statsBackground.SetActive(false);
        }
        else if (linkedCard is LandCard)
        {
            costText.text = "";
            statsText.text = "";
            keywordText.text = "";

            costBackground.SetActive(false);
            statsBackground.SetActive(false);
        }
        else
        {
            costText.text = "";
            statsText.text = "";
            keywordText.text = "";

            costBackground.SetActive(false);
            statsBackground.SetActive(false);
        }

        UpdateVisual();

    }

    public void OnClick()
    {
        if (GameManager.Instance != null && GameManager.Instance.graveyardViewActive)
        {
            if (isInGraveyardViewer && linkedCard is CreatureCard graveCreature &&
                GameManager.Instance.humanPlayer.Graveyard.Contains(linkedCard) &&
                graveCreature.activatedAbilities != null)
            {
                Player player = GameManager.Instance.humanPlayer;
                int cost = graveCreature.manaToPayToActivate;
                if (graveCreature.activatedAbilities.Contains(ActivatedAbility.ReturnSelfFromGraveyard))
                {
                    if (player.ColoredMana.Total() >= cost)
                    {
                        player.ColoredMana.SpendGeneric(cost);
                        GameManager.Instance.QueueCreatureActivatedAbility(graveCreature, ActivatedAbility.ReturnSelfFromGraveyard, player);
                    }
                    else
                    {
                        Debug.Log($"Not enough mana to return {graveCreature.cardName} from the graveyard.");
                    }
                }
                else if (graveCreature.activatedAbilities.Contains(ActivatedAbility.ReturnSelfFromGraveyardToHand))
                {
                    if (player.ColoredMana.Total() >= cost)
                    {
                        player.ColoredMana.SpendGeneric(cost);
                        GameManager.Instance.QueueCreatureActivatedAbility(graveCreature, ActivatedAbility.ReturnSelfFromGraveyardToHand, player);
                    }
                    else
                    {
                        Debug.Log($"Not enough mana to return {graveCreature.cardName} from the graveyard.");
                    }
                }
            }
            return;
        }
        if (SceneManager.GetActiveScene().name == "DeckBuilderScene")
        {
            DeckGenerator dg = FindObjectOfType<DeckGenerator>();
            if (dg != null)
                dg.RerollCard(transform.GetSiblingIndex());
            return;
        }
        if (SceneManager.GetActiveScene().name == "DeckEditorScene")
        {
            var mgr = FindObjectOfType<DeckEditorManager>();
            if (mgr != null)
            {
                CardData data = CardDatabase.GetCardData(linkedCard.cardName);
                mgr.OnCardClicked(data, gameObject);
            }
            return;
        }
        if (isInStack)
            return;
        // Optional ETB targeting (e.g. Monk: "You may destroy target artifact")
        if (GameManager.Instance.targetingCreatureOptional != null)
        {
            Card clicked = linkedCard;
            var ability = GameManager.Instance.optionalAbility;
            bool isValid = false;

            if ((ability.requiredTargetType == SorceryCard.TargetType.Creature ||
                 ability.requiredTargetType == SorceryCard.TargetType.CreatureOrPlayer) &&
                clicked is CreatureCard targetCreature)
                isValid = !(ability.excludeArtifactCreatures && targetCreature.color.Contains("Artifact"));

            if (ability.requiredTargetType == SorceryCard.TargetType.Artifact && clicked is ArtifactCard)
                isValid = true;

            if (ability.requiredTargetType == SorceryCard.TargetType.Enchantment && clicked is EnchantmentCard)
                isValid = true;

            if (ability.requiredTargetType == SorceryCard.TargetType.Land && clicked is LandCard)
                isValid = true;

            if (isValid && GameManager.Instance.GetOwnerOfCard(clicked)?.Battlefield.Contains(clicked) == true)
            {
                GameManager.Instance.ResolveOptionalTargeting(clicked);
            }
            else
            {
                Debug.Log("Clicked invalid target — optional ETB does nothing.");
                GameManager.Instance.CancelOptionalTargeting();
            }
            return;
        }

        if (GameManager.Instance.targetingArtifact != null)
        {
            Card clicked = linkedCard;

            if (clicked is CreatureCard)
            {
                GameManager.Instance.CompleteTargetSelection(this);
            }
            else
            {
                Debug.Log("Clicked non-creature during artifact targeting — cancelling.");
                GameManager.Instance.CancelTargeting();
            }

            return;
        }

        if (GameManager.Instance.targetingEquipment != null)
        {
            if (linkedCard is CreatureCard equippedCreature &&
                GameManager.Instance.GetOwnerOfCard(equippedCreature) == GameManager.Instance.targetingPlayer)
            {
                GameManager.Instance.CompleteTargetSelection(this);
            }
            else
            {
                Debug.Log("Invalid target for equipment.");
                GameManager.Instance.CancelTargeting();
            }

            return;
        }
        if (GameManager.Instance.targetingAura != null)
        {
            if (linkedCard is CreatureCard linkedCreature)
            {
                var aura = GameManager.Instance.targetingAura;
                bool valid = (aura.requiredTargetType == SorceryCard.TargetType.Creature ||
                              (aura.requiredTargetType == SorceryCard.TargetType.TappedCreature && linkedCreature.isTapped)) &&
                             (!aura.targetMustBeControlledCreature ||
                              GameManager.Instance.GetOwnerOfCard(linkedCreature) == GameManager.Instance.targetingPlayer);
                if (valid)
                {
                    GameManager.Instance.CompleteTargetSelection(this);
                }
                else
                {
                    Debug.Log("Invalid target for aura.");
                    GameManager.Instance.CancelTargeting();
                }
            }
            else
            {
                Debug.Log("Invalid target for aura.");
                GameManager.Instance.CancelTargeting();
            }

            return;
        }
        if (GameManager.Instance.targetingSorcery != null)
        {
            if (GameManager.Instance.targetingVisual == this)
            {
                GameManager.Instance.CancelTargeting();
                return;
            }

            var spell = GameManager.Instance.targetingSorcery;
            var caster = GameManager.Instance.targetingPlayer;
            var card = linkedCard;

            bool valid = false;

            // Creature target
            if ((spell.requiredTargetType == SorceryCard.TargetType.Creature ||
                spell.requiredTargetType == SorceryCard.TargetType.CreatureOrPlayer) &&
                card is CreatureCard targetCreature)
            {
                // Is on battlefield and not protected
                if (GameManager.Instance.GetOwnerOfCard(card).Battlefield.Contains(card))
                {
                    var protection = ProtectionUtils.GetProtectionKeyword(spell.PrimaryColor);
                    bool notArtifact = !(spell.excludeArtifactCreatures && targetCreature.color.Contains("Artifact"));
                    if (!targetCreature.keywordAbilities.Contains(protection) && notArtifact)
                        valid = true;
                }
            }

            // Land / Artifact: not allowed unless explicitly targeted
            if (spell.requiredTargetType == SorceryCard.TargetType.Land && card is LandCard)
            {
                if (GameManager.Instance.GetOwnerOfCard(card).Battlefield.Contains(card))
                    valid = true;
            }

            if (spell.requiredTargetType == SorceryCard.TargetType.Artifact && card is ArtifactCard)
            {
                if (GameManager.Instance.GetOwnerOfCard(card).Battlefield.Contains(card))
                    valid = true;
            }

            if (spell.requiredTargetType == SorceryCard.TargetType.Enchantment && card is EnchantmentCard)
            {
                if (GameManager.Instance.GetOwnerOfCard(card).Battlefield.Contains(card))
                    valid = true;
            }

            if (valid)
            {
                GameManager.Instance.CompleteTargetSelection(this);
            }
            else
            {
                Debug.Log("Invalid target — canceling.");
                GameManager.Instance.CancelTargeting();
            }

            return;
        }

        if (!isInBattlefield && GameManager.Instance.humanPlayer.Hand.Contains(linkedCard) == false)
        {
            Debug.Log("Cannot play or interact with cards in the graveyard.");
            return;
        }

        TurnSystem.TurnPhase phase = TurnSystem.Instance.currentPhase;
        bool canActivateArtifact =
            (
                (TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.Human &&
                    (phase == TurnSystem.TurnPhase.Main1 ||
                     phase == TurnSystem.TurnPhase.Main2 ||
                     phase == TurnSystem.TurnPhase.ConfirmAttackers ||
                     phase == TurnSystem.TurnPhase.ConfirmBlockers)) ||
                (TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.AI &&
                    (phase == TurnSystem.TurnPhase.ChooseBlockers ||
                     phase == TurnSystem.TurnPhase.ConfirmBlockers))
            ) && !GameManager.Instance.IsStackActive();

        if (linkedCard.activatedAbilities != null &&
        linkedCard.activatedAbilities.Contains(ActivatedAbility.TapForMana) &&
        !linkedCard.isTapped &&
        GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
        TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.Human &&
        (TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main1 || TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main2) &&
        (!(linkedCard is CreatureCard cc) || !cc.hasSummoningSickness || cc.keywordAbilities.Contains(KeywordAbility.Haste)))
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.tap_for_mana);
            linkedCard.isTapped = true;

            string color = linkedCard.PrimaryColor;

            switch (color)
            {
                case "White": GameManager.Instance.humanPlayer.ColoredMana.White++; break;
                case "Blue": GameManager.Instance.humanPlayer.ColoredMana.Blue++; break;
                case "Black": GameManager.Instance.humanPlayer.ColoredMana.Black++; break;
                case "Red": GameManager.Instance.humanPlayer.ColoredMana.Red++; break;
                case "Green": GameManager.Instance.humanPlayer.ColoredMana.Green++; break;
                default:
                    GameManager.Instance.humanPlayer.ColoredMana.Colorless++;
                    Debug.LogWarning($"{linkedCard.cardName} has no valid color for mana — added colorless instead.");
                    break;
            }

            GameManager.Instance.UpdateUI();
            UpdateVisual();
            return;
        }

        // PAY-TO-GAIN-ABILITY during Main Phase
        if (linkedCard is CreatureCard abilityCreature &&
        GameManager.Instance.humanPlayer.Battlefield.Contains(abilityCreature) &&
        TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.Human &&
        (TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main1 || TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main2) &&
        abilityCreature.activatedAbilities != null &&
        abilityCreature.activatedAbilities.Contains(ActivatedAbility.PayToGainAbility))
        {
            Player player = GameManager.Instance.humanPlayer;
            int cost = abilityCreature.manaToPayToActivate;
            string cardColor = abilityCreature.PrimaryColor;

            // Colored ability: must pay using that color (plus generic if cost > 1)
            if (!string.IsNullOrEmpty(cardColor) && cardColor != "Artifact")
            {
                int available = cardColor switch
                {
                    "White" => player.ColoredMana.White,
                    "Blue" => player.ColoredMana.Blue,
                    "Black" => player.ColoredMana.Black,
                    "Red" => player.ColoredMana.Red,
                    "Green" => player.ColoredMana.Green,
                    _ => 0
                };

                int remaining = cost - 1;
                if (available >= 1 && player.ColoredMana.Total() - available >= remaining)
                {
                    GameManager.Instance.QueueCreatureActivatedAbility(abilityCreature, ActivatedAbility.PayToGainAbility, player);
                }
                else
                {
                    Debug.Log($"Not enough {cardColor} + generic mana to activate {abilityCreature.cardName}'s ability.");
                }
            }
            else // Colorless/generic activation
            {
                if (player.ColoredMana.Total() >= cost)
                {
                    GameManager.Instance.QueueCreatureActivatedAbility(abilityCreature, ActivatedAbility.PayToGainAbility, player);
                }
                else
                {
                    Debug.Log($"Not enough generic mana to activate {abilityCreature.cardName}'s ability.");
                }
            }
            return;
        }

        // PAY-TO-BUFF-SELF during Main Phase
        if (linkedCard is CreatureCard pumpCreature &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(pumpCreature) &&
            TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.Human &&
            (TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main1 || TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main2) &&
            pumpCreature.activatedAbilities != null &&
            pumpCreature.activatedAbilities.Contains(ActivatedAbility.PayToBuffSelf))
        {
            Player player = GameManager.Instance.humanPlayer;
            int cost = pumpCreature.manaToPayToActivate;
            string color = pumpCreature.PrimaryColor;

            if (!string.IsNullOrEmpty(color) && color != "Artifact")
            {
                int available = color switch
                {
                    "White" => player.ColoredMana.White,
                    "Blue" => player.ColoredMana.Blue,
                    "Black" => player.ColoredMana.Black,
                    "Red" => player.ColoredMana.Red,
                    "Green" => player.ColoredMana.Green,
                    _ => 0
                };

                if (available >= cost)
                {
                    GameManager.Instance.QueueCreatureActivatedAbility(pumpCreature, ActivatedAbility.PayToBuffSelf, player);
                }
                else
                {
                    Debug.Log($"Not enough {color} mana to activate {pumpCreature.cardName}'s ability.");
                }
            }
            else
            {
                if (player.ColoredMana.Total() >= cost)
                {
                    GameManager.Instance.QueueCreatureActivatedAbility(pumpCreature, ActivatedAbility.PayToBuffSelf, player);
                }
                else
                {
                    Debug.Log($"Not enough mana to activate {pumpCreature.cardName}'s ability.");
                }
            }

            return;
        }


        // TAP-TO-CREATE-TOKEN generic ability
        if (linkedCard.activatedAbilities.Contains(ActivatedAbility.TapToCreateToken) &&
            linkedCard is CreatureCard tokenSpawner &&
            !linkedCard.isTapped &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
            TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.Human &&
            (TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main1 || TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main2) &&
            (!tokenSpawner.hasSummoningSickness || tokenSpawner.keywordAbilities.Contains(KeywordAbility.Haste)))
        {
            Player player = GameManager.Instance.humanPlayer;
            int cost = linkedCard.manaToPayToActivate;

            if (player.ColoredMana.Total() >= cost)
            {
                int remaining = cost;

                // Spend colorless first
                int useColorless = Mathf.Min(player.ColoredMana.Colorless, remaining);
                player.ColoredMana.Colorless -= useColorless;
                remaining -= useColorless;

                // Spend from WUBRG
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.White, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Blue, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Black, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Red, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Green, remaining);

                if (remaining > 0)
                {
                    Debug.Log("Not enough mana to activate token creation.");
                    return;
                }

                linkedCard.isTapped = true;
                GameManager.Instance.QueueCreatureActivatedAbility(tokenSpawner, ActivatedAbility.TapToCreateToken, player);
                GameManager.Instance.UpdateUI();
                UpdateVisual();
            }
            else
            {
                Debug.Log("Not enough mana to activate token creation.");
            }

            return;
        }
        // TAP-TO-LOSE-LIFE ability during Main Phase
        if (linkedCard is CreatureCard creatureForDrain &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(creatureForDrain) &&
            TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.Human &&
            (TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main1 || TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main2) &&
            creatureForDrain.activatedAbilities != null &&
            creatureForDrain.activatedAbilities.Contains(ActivatedAbility.TapToLoseLife) &&
            !creatureForDrain.isTapped &&
            (!creatureForDrain.hasSummoningSickness || creatureForDrain.keywordAbilities.Contains(KeywordAbility.Haste)))
        {
            creatureForDrain.isTapped = true;
            GameManager.Instance.QueueCreatureActivatedAbility(creatureForDrain, ActivatedAbility.TapToLoseLife, GameManager.Instance.humanPlayer);
            UpdateVisual();
            GameManager.Instance.UpdateUI();
            return;
        }

        // TAP-TO-PLAGUE ability
        if (linkedCard.activatedAbilities.Contains(ActivatedAbility.TapToPlague) &&
            !linkedCard.isTapped &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
            canActivateArtifact)
        {
            linkedCard.isTapped = true;
            GameManager.Instance.QueueArtifactActivatedAbility(linkedCard as ArtifactCard, ActivatedAbility.TapToPlague, GameManager.Instance.humanPlayer);
            UpdateVisual();
            GameManager.Instance.UpdateUI();
            return;
        }

        // TAP-AND-SACRIFICE-FOR-MANA or SACRIFICE-FOR-MANA
        if (linkedCard.activatedAbilities != null &&
            (linkedCard.activatedAbilities.Contains(ActivatedAbility.TapAndSacrificeForMana) ||
            linkedCard.activatedAbilities.Contains(ActivatedAbility.SacrificeForMana)) &&
            !linkedCard.isTapped &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
            canActivateArtifact)
        {
            ArtifactCard artifact = linkedCard as ArtifactCard;
            Player player = GameManager.Instance.humanPlayer;

            int cost = artifact.manaToPayToActivate;
            int gain = artifact.manaToGain;

            if (cost > 0)
            {
                if (player.ColoredMana.Total() >= cost)
                {
                    int remaining = cost;

                    // Spend colorless first
                    int useColorless = Mathf.Min(player.ColoredMana.Colorless, remaining);
                    player.ColoredMana.Colorless -= useColorless;
                    remaining -= useColorless;

                    // Spend from WUBRG
                    remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.White, remaining);
                    remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Blue, remaining);
                    remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Black, remaining);
                    remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Red, remaining);
                    remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Green, remaining);

                    if (remaining > 0)
                    {
                        Debug.Log("Not enough mana for ability.");
                        return;
                    }

                    player.ColoredMana.Colorless += gain;
                    Debug.Log($"{linkedCard.cardName} activated: +{gain} mana (paid {cost}).");
                }
                else
                {
                    Debug.Log("Not enough mana for paid activation.");
                    return;
                }
            }
            else
            {
                // Free ability
                player.ColoredMana.Colorless += gain;
                Debug.Log($"{linkedCard.cardName} activated: +{gain} mana (free).");
            }

            // Shared execution
            linkedCard.isTapped = true;
            SoundManager.Instance.PlaySound(SoundManager.Instance.break_artifact);
            GameManager.Instance.SendToGraveyard(linkedCard, player);
            GameManager.Instance.UpdateUI();
            UpdateVisual();
            return;
        }


        // TAP-TO-GAIN-LIFE ability
        if (linkedCard.activatedAbilities != null &&
            linkedCard.activatedAbilities.Contains(ActivatedAbility.TapToGainLife) &&
            !linkedCard.isTapped &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
            canActivateArtifact)
        {
            linkedCard.isTapped = true;
            GameManager.Instance.QueueArtifactActivatedAbility(linkedCard as ArtifactCard, ActivatedAbility.TapToGainLife, GameManager.Instance.humanPlayer);
            UpdateVisual();
            GameManager.Instance.UpdateUI();
            return;
        }

        // SACRIFICE-FOR-LIFE
        if (linkedCard.activatedAbilities != null &&
            linkedCard.activatedAbilities.Contains(ActivatedAbility.SacrificeForLife) &&
            !linkedCard.isTapped &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
            canActivateArtifact)
        {
            ArtifactCard artifact = linkedCard as ArtifactCard;
            Player player = GameManager.Instance.humanPlayer;
            int remaining = artifact.manaToPayToActivate;

            if (player.ColoredMana.Total() >= remaining)
            {
                int useColorless = Mathf.Min(player.ColoredMana.Colorless, remaining);
                player.ColoredMana.Colorless -= useColorless;
                remaining -= useColorless;

                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.White, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Blue, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Black, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Red, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Green, remaining);

                if (remaining > 0)
                {
                    Debug.Log("Not enough mana for ability.");
                    return;
                }

                linkedCard.isTapped = true;
                GameManager.Instance.SendToGraveyard(linkedCard, player);
                GameManager.Instance.QueueArtifactActivatedAbility(artifact, ActivatedAbility.SacrificeForLife, player);
                GameManager.Instance.UpdateUI();
                UpdateVisual();

                Debug.Log($"{linkedCard.cardName} activated: Gain {artifact.lifeToGain} life.");
            }
            else
            {
                Debug.Log("Not enough mana for ability.");
            }

            return;
        }

        //SACRIFICE TO DEAL DAMAGE
        if (linkedCard.activatedAbilities != null &&
            linkedCard.activatedAbilities.Contains(ActivatedAbility.DealDamageToCreature) &&
            !linkedCard.isTapped &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
            canActivateArtifact)
        {
            // Check for mana
            ArtifactCard artifact = linkedCard as ArtifactCard;
            Player player = GameManager.Instance.humanPlayer;
            int totalAvailable = player.ColoredMana.Total();
            int cost = artifact.manaToPayToActivate;

            if (totalAvailable >= cost)
            {
                GameManager.Instance.BeginTargetingWithArtifactDamage(artifact, player, this);
            }
            else
            {
                Debug.Log("Not enough mana to activate damage artifact.");
            }

            return;
        }

        // SACRIFICE TO BUFF CREATURE
        if (linkedCard.activatedAbilities != null &&
            linkedCard.activatedAbilities.Contains(ActivatedAbility.BuffTargetCreature) &&
            !linkedCard.isTapped &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
            canActivateArtifact)
        {
            ArtifactCard artifact = linkedCard as ArtifactCard;
            Player player = GameManager.Instance.humanPlayer;
            int totalAvailable = player.ColoredMana.Total();
            int cost = artifact.manaToPayToActivate;

            if (totalAvailable >= cost)
            {
                GameManager.Instance.BeginTargetingWithArtifactBuff(artifact, player, this);
            }
            else
            {
                Debug.Log("Not enough mana to activate buff artifact.");
            }

            return;
        }

        // EQUIP artifact during Main Phase
        if (linkedCard is EquipmentCard equipment &&
            linkedCard.activatedAbilities.Contains(ActivatedAbility.Equip) &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
            TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.Human &&
            (TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main1 || TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.Main2))
        {
            Player player = GameManager.Instance.humanPlayer;
            int cost = equipment.EquipCost;
            if (player.ColoredMana.Total() >= cost)
            {
                GameManager.Instance.BeginEquipmentTargetSelection(equipment, player, this);
            }
            else
            {
                Debug.Log("Not enough mana to equip.");
            }

            return;
        }

        // SACRIFICE-TO-DRAW-CARDS
        if (linkedCard.activatedAbilities != null &&
            linkedCard.activatedAbilities.Contains(ActivatedAbility.SacrificeToDrawCards) &&
            !linkedCard.isTapped &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
            canActivateArtifact)
        {
            ArtifactCard artifact = linkedCard as ArtifactCard;

            Player player = GameManager.Instance.humanPlayer;
            int totalAvailable = player.ColoredMana.Total();
            int cost = artifact.manaToPayToActivate;

            if (totalAvailable >= cost)
            {
                int remaining = cost;

                // 1. Spend colorless first
                int useColorless = Mathf.Min(player.ColoredMana.Colorless, remaining);
                player.ColoredMana.Colorless -= useColorless;
                remaining -= useColorless;

                // 2. Spend from WUBRG
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.White, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Blue, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Black, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Red, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Green, remaining);

                if (remaining > 0)
                {
                    Debug.LogWarning("Draw ability: Not enough mana even though initial check passed.");
                    return;
                }
                linkedCard.isTapped = true;
                GameManager.Instance.SendToGraveyard(linkedCard, player);
                GameManager.Instance.QueueArtifactActivatedAbility(artifact, ActivatedAbility.SacrificeToDrawCards, player);
                GameManager.Instance.UpdateUI();
                UpdateVisual();
                Debug.Log($"{linkedCard.cardName} activated: Draw {artifact.cardsToDraw} cards.");
            }
            else
            {
                Debug.Log("Not enough mana to activate draw ability.");
            }

            return;
        }

        // TAP TO PLAY RANDOM POTION
        if (linkedCard.activatedAbilities != null &&
            linkedCard.activatedAbilities.Contains(ActivatedAbility.TapToPlayRandomPotion) &&
            !linkedCard.isTapped &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(linkedCard) &&
            canActivateArtifact)
        {
            ArtifactCard artifact = linkedCard as ArtifactCard;
            Player player = GameManager.Instance.humanPlayer;
            int cost = artifact.manaToPayToActivate;

            if (player.ColoredMana.Total() >= cost)
            {
                int remaining = cost;

                int useColorless = Mathf.Min(player.ColoredMana.Colorless, remaining);
                player.ColoredMana.Colorless -= useColorless;
                remaining -= useColorless;

                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.White, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Blue, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Black, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Red, remaining);
                remaining -= Player.ManaPool.SpendFromPool(ref player.ColoredMana.Green, remaining);

                if (remaining > 0)
                {
                    Debug.Log("Not enough mana for ability.");
                    return;
                }
                linkedCard.isTapped = true;
                GameManager.Instance.QueueArtifactActivatedAbility(artifact, ActivatedAbility.TapToPlayRandomPotion, player);
                GameManager.Instance.UpdateUI();
                UpdateVisual();
            }
            else
            {
                Debug.Log("Not enough mana to activate ability.");
            }

            return;
        }

        // Blocking phase
        if (TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.ChooseBlockers &&
            TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.AI)

            if (linkedCard is CreatureCard clickedCreature)
            {
                Player you = GameManager.Instance.humanPlayer;
                Player enemy = GameManager.Instance.aiPlayer;

                // If you click your own untapped creature first
                if (you.Battlefield.Contains(clickedCreature) &&
                    !clickedCreature.isTapped &&
                    !clickedCreature.keywordAbilities.Contains(KeywordAbility.CantBlock) &&
                    !(
                        clickedCreature.keywordAbilities.Contains(KeywordAbility.CantBlockWithoutForest) &&
                        !you.Battlefield.Exists(card => card is LandCard land && land.cardName.ToLower().Contains("forest"))
                    ))
                {
                    // Already blocking something -> remove block
                    if (clickedCreature.blockingThisAttacker != null)
                    {
                        Debug.Log($"{clickedCreature.cardName} stops blocking.");
                        clickedCreature.blockingThisAttacker.blockedByThisBlocker.Remove(clickedCreature);
                        clickedCreature.blockingThisAttacker = null;
                        GameManager.Instance.UpdateUI();
                        return;
                    }

                    GameManager.Instance.selectedBlockerForBlocking = clickedCreature;
                    Debug.Log($"Selected blocker: {clickedCreature.cardName}");
                    return;
                }

                // If you click an attacker after selecting a blocker
                if (enemy.Battlefield.Contains(clickedCreature) &&
                    GameManager.Instance.currentAttackers.Contains(clickedCreature))
                {
                    var blocker = GameManager.Instance.selectedBlockerForBlocking;

                    if (blocker == null)
                    {
                        Debug.Log("Click one of your untapped creatures first to block.");
                        return;
                    }

                    // Check if this blocker is allowed to block the attacker (Flying rule)
                    if (clickedCreature.keywordAbilities.Contains(KeywordAbility.Flying) &&
                        !blocker.keywordAbilities.Contains(KeywordAbility.Flying) &&
                        !blocker.keywordAbilities.Contains(KeywordAbility.Reach))
                    {
                        Debug.Log($"{blocker.cardName} can't block {clickedCreature.cardName} because it lacks Flying or Reach.");
                        return;
                    }

                    // Check if blocker is restricted to flying-only
                    if (blocker.keywordAbilities.Contains(KeywordAbility.CanOnlyBlockFlying) &&
                        !clickedCreature.keywordAbilities.Contains(KeywordAbility.Flying))
                    {
                        Debug.Log($"{blocker.cardName} can only block flying creatures.");
                        return;
                    }
                    // Check if attacker is unblockable due to landwalk
                    if (IsLandwalkPreventingBlock(clickedCreature, you))
                    {
                        Debug.Log($"{blocker.cardName} can't block {clickedCreature.cardName} due to landwalk.");
                        return;
                    }
                    // Check if haste creatures can only be blocked by haste
                    if (GameManager.Instance.IsHasteCreaturesOnlyBlockedByHasteActive(enemy) &&
                        clickedCreature.keywordAbilities.Contains(KeywordAbility.Haste) &&
                        !blocker.keywordAbilities.Contains(KeywordAbility.Haste))
                    {
                        Debug.Log($"{clickedCreature.cardName} can only be blocked by creatures with haste.");
                        return;
                    }
                    // Prevent blocking if attacker has protection from blocker's color
                    if (blocker.color.Any(c => clickedCreature.keywordAbilities.Contains(ProtectionUtils.GetProtectionKeyword(c))))
                    {
                        Debug.Log($"{clickedCreature.cardName} has protection from {blocker.color}, so it can't be blocked by {blocker.cardName}.");
                        return;
                    }

                    // Assign the block
                    blocker.blockingThisAttacker = clickedCreature;
                    clickedCreature.blockedByThisBlocker.Add(blocker);
                    GameManager.Instance.selectedBlockerForBlocking = null;

                    Debug.Log($"{blocker.cardName} is blocking {clickedCreature.cardName}");
                    SoundManager.Instance.PlaySound(SoundManager.Instance.declareBlock);
                    GameManager.Instance.UpdateUI();
                    return;
                }
            }

        // Declare attacker
        if (TurnSystem.Instance.currentPhase == TurnSystem.TurnPhase.ChooseAttackers &&
            TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.Human &&
            linkedCard is CreatureCard creature &&
            GameManager.Instance.humanPlayer.Battlefield.Contains(creature))
        {
            if (GameManager.Instance.selectedAttackers.Contains(creature))
            {
                // Removing from combat
                GameManager.Instance.selectedAttackers.Remove(creature);
                if (!creature.keywordAbilities.Contains(KeywordAbility.Vigilance))
                    creature.isTapped = false;

                Debug.Log($"{creature.cardName} removed from attackers.");
            }
            else if (!creature.hasSummoningSickness && !creature.keywordAbilities.Contains(KeywordAbility.Defender))
            {
                // Adding to combat
                GameManager.Instance.selectedAttackers.Add(creature);
                SoundManager.Instance.PlaySound(SoundManager.Instance.declareAttack);
                if (!creature.keywordAbilities.Contains(KeywordAbility.Vigilance))
                    creature.isTapped = true;

                Debug.Log($"{creature.cardName} declared as attacker.");
            }

            UpdateVisual();
            return;
        }

        // Land usage
        if (isInBattlefield && linkedCard is LandCard land)
        {
            if (GameManager.Instance.GetOwnerOfCard(land) == GameManager.Instance.humanPlayer)
            {
                GameManager.Instance.TapLandForMana(land, GameManager.Instance.humanPlayer);
                UpdateVisual();
            }
            return;
        }

        // Playing card from hand
        if (!isInBattlefield)
        {
            CardData cardData = CardDatabase.GetCardData(linkedCard.cardName);
            bool isInstant = cardData != null && cardData.cardType == CardType.Instant;

            var currentPhase = TurnSystem.Instance.currentPhase;
            bool mainPhase = currentPhase == TurnSystem.TurnPhase.Main1 || currentPhase == TurnSystem.TurnPhase.Main2;
            bool combatPhase = currentPhase == TurnSystem.TurnPhase.ConfirmAttackers || currentPhase == TurnSystem.TurnPhase.ConfirmBlockers;
            bool humansTurn = TurnSystem.Instance.currentPlayer == TurnSystem.PlayerType.Human;

            if (!isInstant)
            {
                if (!humansTurn || !mainPhase)
                {
                    Debug.Log("You can only play cards during your own Main Phase.");
                    return;
                }
            }
            else
            {
                bool validTime = (humansTurn && (mainPhase || combatPhase)) || (!humansTurn && combatPhase);
                if (GameManager.Instance.IsStackActive() || !validTime)
                {
                    Debug.Log("Instants can only be cast during your main phases or any combat when the stack is empty.");
                    return;
                }
            }

            GameManager.Instance.CancelOptionalTargeting(); // cancel any other ETB clicks

            GameManager.Instance.PlayCard(GameManager.Instance.humanPlayer, this);
            UpdateVisual();
            return;
        }
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

    public void ResetVisualForGraveyard()
    {
        isInBattlefield = false;
        isInGraveyard = true;
        isInStack = false;

        if (lineRenderer != null)
            lineRenderer.enabled = false;
        if (swordIcon != null) swordIcon.SetActive(false);
        if (shieldIcon != null) shieldIcon.SetActive(false);

        transform.localScale = Vector3.one * 0.5f;
        transform.rotation = Quaternion.identity;

        costBackground.SetActive(false);
        statsBackground.SetActive(false);

        sicknessText.text = "";
        keywordText.text = "";
        statsText.text = "";
        if (artistText != null)
        {
            artistText.text = "";
            artistText.gameObject.SetActive(false);
        }
    }

    public void PrepareForGraveyard()
    {
        isInBattlefield = false;
        isInGraveyard = true;
        isInStack = false;

        // Re-enable UI components in case they were disabled on battlefield
        if (backgroundImage != null) backgroundImage.enabled = true;
        if (titleText != null) titleText.enabled = true;
        if (sicknessText != null) sicknessText.enabled = true;
        if (costText != null) costText.enabled = true;
        if (statsText != null) statsText.enabled = true;
        if (keywordText != null) keywordText.enabled = true;
        if (artistText != null) artistText.enabled = true;

        costBackground.SetActive(true);
        statsBackground.SetActive(true);

        if (lineRenderer != null) lineRenderer.enabled = false;
        if (swordIcon != null) swordIcon.SetActive(false);
        if (shieldIcon != null) shieldIcon.SetActive(false);

        // Reset rotation & scale
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one * 0.5f;
    }

    public void UpdateGraveyardVisual()
    {
        // General setup
        isInBattlefield = false;
        isInGraveyard = true;
        isInStack = false;

        CardData cardData = CardDatabase.GetCardData(linkedCard.cardName);
        if (cardData != null && cardTypeText != null)
        {
            string typeLine;

            if (cardData.cardType == CardType.Creature)
            {
                bool isArtifactCreature = cardData.color != null && cardData.color.Contains("Artifact");
                string baseType = isArtifactCreature ? "Artifact Creature" : "Creature";

                if (cardData.subtypes != null && cardData.subtypes.Count > 0)
                    typeLine = $"{baseType} — {string.Join(" ", cardData.subtypes)}";
                else
                    typeLine = baseType;
            }
            else if (cardData.cardType == CardType.Enchantment && cardData.subtypes != null && cardData.subtypes.Count > 0)
            {
                typeLine = $"Enchantment — {string.Join(" ", cardData.subtypes)}"; // e.g. Aura
            }
            else if (cardData.cardType == CardType.Artifact && cardData.subtypes != null && cardData.subtypes.Count > 0)
            {
                typeLine = $"Artifact — {string.Join(" ", cardData.subtypes)}";
            }
            else
            {
                typeLine = cardData.cardType.ToString(); // e.g. "Artifact", "Land", etc.
            }

            cardTypeText.text = typeLine;
            cardTypeText.enabled = !isInBattlefield;
        }

        transform.localScale = Vector3.one * 0.5f;
        transform.rotation = Quaternion.identity;

        // Reset stats position (in case it was moved on battlefield)
        if (statsBackground != null)
        {
            RectTransform statsRect = statsBackground.GetComponent<RectTransform>();
            if (statsRect != null)
                statsRect.anchoredPosition = defaultStatsPosition;
        }

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        // Enable all UI elements
        if (backgroundImage != null) backgroundImage.enabled = true;
        if (titleText != null) titleText.enabled = true;
        if (sicknessText != null) sicknessText.enabled = true;
        if (costText != null) costText.enabled = true;
        if (statsText != null) statsText.enabled = true;
        if (keywordText != null) keywordText.enabled = true;
        if (cardRarity != null) cardRarity.enabled = true;
        if (artistText != null) artistText.enabled = true;

        titleText.text = linkedCard.cardName;
        sicknessText.text = "";
        lineRenderer.enabled = false;
        if (swordIcon != null) swordIcon.SetActive(false);
        if (shieldIcon != null) shieldIcon.SetActive(false);

        if (landIcon != null)
        {
            if (linkedCard is LandCard && !isInBattlefield)
            {
                Sprite landSprite = linkedCard.PrimaryColor switch
                {
                    "White" => whiteLandIcon,
                    "Blue" => blueLandIcon,
                    "Black" => blackLandIcon,
                    "Red" => redLandIcon,
                    "Green" => greenLandIcon,
                    _ => null
                };

                landIcon.GetComponent<Image>().sprite = landSprite;
                landIcon.SetActive(landSprite != null);
            }
            else
            {
                landIcon.SetActive(false);
            }
        }


        // Load card data
        CardData sourceData = CardDatabase.GetCardData(linkedCard.cardName);
        SetCardBorder(sourceData);

        if (artistText != null)
        {
            if (sourceData != null && !string.IsNullOrEmpty(sourceData.artist))
            {
                artistText.text = sourceData.artist;
                artistText.gameObject.SetActive(true);
            }
            else
            {
                artistText.gameObject.SetActive(false);
            }
        }

        if (artImage != null && linkedCard.artwork != null)
            artImage.sprite = linkedCard.artwork;

        // Show correct info by card type
        if (linkedCard is CreatureCard creature)
        {
            costText.text = Mathf.Max(creature.manaCost - linkedCard.color.Count, 0).ToString();
            statsText.text = $"{creature.power}/{creature.toughness}";
            keywordText.text = linkedCard.GetCardText();

            costBackground.SetActive(true);
            statsBackground.SetActive(true);
        }
        else if (linkedCard is ArtifactCard artifact)
        {
            costText.text = artifact.manaCost.ToString();
            statsText.text = "";
            keywordText.text = linkedCard.GetCardText();

            costBackground.SetActive(true);
            statsBackground.SetActive(false);
        }
        else if (linkedCard is SorceryCard sorcery)
        {
            costText.text = Mathf.Max(sorcery.manaCost - linkedCard.color.Count, 0).ToString();
            statsText.text = "";

            sorceryEffect(sorcery);

            costBackground.SetActive(true);
            statsBackground.SetActive(false);
        }
        else if (linkedCard is EnchantmentCard enchantment)
        {
            costText.text = Mathf.Max(enchantment.manaCost - linkedCard.color.Count, 0).ToString();
            statsText.text = "";
            keywordText.text = linkedCard.GetCardText();

            costBackground.SetActive(true);
            statsBackground.SetActive(false);
        }
        else if (linkedCard is LandCard)
        {
            costText.text = "";
            statsText.text = "";
            keywordText.text = "";

            costBackground.SetActive(false);
            statsBackground.SetActive(false);
        }
        else
        {
            // Unknown type — hide everything
            costText.text = "";
            statsText.text = "";
            keywordText.text = "";

            costBackground.SetActive(false);
            statsBackground.SetActive(false);
        }
    }

    private void SetCardBorder(CardData data)
    {
        if (backgroundImage == null || data == null) return;

        if (data.cardType == CardType.Land)
        {
            backgroundImage.sprite = landBorder;
        }
        else if (data.cardType == CardType.Artifact || data.color.Contains("Artifact") || data.color.Count == 0)
        {
            backgroundImage.sprite = artifactBorder;
        }
        else if (data.color.Count > 1)
        {
            backgroundImage.sprite = multicolorBorder;
        }
        else
        {
            switch (data.color[0])
            {
                case "White": backgroundImage.sprite = whiteBorder; break;
                case "Blue": backgroundImage.sprite = blueBorder; break;
                case "Black": backgroundImage.sprite = blackBorder; break;
                case "Red": backgroundImage.sprite = redBorder; break;
                case "Green": backgroundImage.sprite = greenBorder; break;
                default: backgroundImage.sprite = defaultBorder; break;
            }
        }

        backgroundImage.color = Color.white; // Prevent leftover tint
    }

    private Color GetRarityColor(string rarity)
    {
        switch (rarity.ToLower())
        {
            case "common": return Color.black;
            case "uncommon": return Color.gray;
            case "rare": return new Color(1f, 0.84f, 0f); // gold
            case "mythic": return new Color(0.5f, 0f, 0.5f); // purple
            default: return Color.clear;
        }
    }

    void sorceryEffect(SorceryCard sorcery)
    {
        string rules = "";

        if (!string.IsNullOrEmpty(sorcery.rulesText))
            rules += sorcery.rulesText + "\n";

        if (sorcery.lifeToGain > 0)
            rules += $"Gain {sorcery.lifeToGain} life.\n";
        if (sorcery.lifeToLoseForOpponent > 0)
            rules += $"Opponent loses {sorcery.lifeToLoseForOpponent} life.\n";
        if (sorcery.lifeLossForBothPlayers > 0)
            rules += $"Each player loses {sorcery.lifeLossForBothPlayers} life.\n";
        if (sorcery.cardsToDrawMax > 0)
        {
            int min = sorcery.cardsToDrawMin;
            int max = sorcery.cardsToDrawMax;
            if (min == max)
                rules += $"Draw {min} card(s).\n";
            else
                rules += $"Draw {min}-{max} cards.\n";
        }
        else if (sorcery.cardsToDraw > 0)
            rules += $"Draw {sorcery.cardsToDraw} card(s).\n";
        if (!string.IsNullOrEmpty(sorcery.tokenToCreate) && sorcery.numberOfTokensMax > 0)
        {
            int min = sorcery.numberOfTokensMin;
            int max = sorcery.numberOfTokensMax;

            if (min == max)
            {
                string plural = (min == 1) ? "" : "s";
                rules += $"Create {min} {sorcery.tokenToCreate} token{plural}.\n";
            }
            else
            {
                rules += $"Create {min}–{max} {sorcery.tokenToCreate} tokens.\n";
            }
        }
        if (sorcery.manaToGainMax > 0)
        {
            int min = sorcery.manaToGainMin;
            int max = sorcery.manaToGainMax;

            string colorName = sorcery.PrimaryColor.ToLower();
            if (min == max)
            {
                rules += $"Add {min} {colorName} mana.\n";
            }
            else
            {
                rules += $"Add {min}-{max} {colorName} mana.\n";
            }
        }
        if (sorcery.cardsToDiscardorDraw > 0)
        {
            rules += $"Opponent discards {sorcery.cardsToDiscardorDraw} card(s) at random.";
            if (sorcery.drawIfOpponentCantDiscard)
                rules += " If can't, you draw a card.";
            rules += "\n";
        }
        if (sorcery.eachPlayerGainLifeEqualToLands)
            rules += $"Each player gains life equal to the number of lands they control.\n";
        if ((sorcery.damageToTarget > 0 || sorcery.damageToTargetMax > 0) &&
            (sorcery.requiredTargetType == SorceryCard.TargetType.Creature ||
            sorcery.requiredTargetType == SorceryCard.TargetType.Player ||
            sorcery.requiredTargetType == SorceryCard.TargetType.CreatureOrPlayer))
        {
            string targetTypeStr = sorcery.requiredTargetType switch
            {
                SorceryCard.TargetType.Creature => "target creature",
                SorceryCard.TargetType.Player => "target player",
                SorceryCard.TargetType.CreatureOrPlayer => "any target",
                _ => "target"
            };
            if (sorcery.damageToTargetMax > 0)
            {
                int min = sorcery.damageToTargetMin;
                int max = sorcery.damageToTargetMax;
                if (min == max)
                    rules += $"Deal {min} damage to {targetTypeStr}.\n";
                else
                    rules += $"Deal {min}-{max} damage to {targetTypeStr}.\n";
            }
            else
            {
                rules += $"Deal {sorcery.damageToTarget} damage to {targetTypeStr}.\n";
            }
        }
        if (sorcery.typeOfPermanentToDestroyAll != SorceryCard.PermanentTypeToDestroy.None)
        {
            string typeStr = sorcery.typeOfPermanentToDestroyAll switch
            {
                SorceryCard.PermanentTypeToDestroy.Land => "lands",
                SorceryCard.PermanentTypeToDestroy.Creature => "creatures",
                SorceryCard.PermanentTypeToDestroy.Artifact => "artifacts",
                SorceryCard.PermanentTypeToDestroy.Enchantment => "enchantments",
                _ => "permanents"
            };
            rules += $"Destroy all {typeStr}.\n";
        }
        if (sorcery.destroyAllWithSameName)
            rules += "Destroy target creature and each other creature with the same name.\n";
        if (sorcery.exileAllCreaturesFromGraveyards)
            rules += "Exile all creature cards from all graveyards.\n";
        if (sorcery.damageToEachCreatureAndPlayer > 0)
            rules += $"Deal {sorcery.damageToEachCreatureAndPlayer} damage to each creature and each player.\n";
        if (sorcery.swapGraveyardAndLibrary)
            rules += "Each player exchanges their graveyard with their library, then shuffles their deck.\n";
        if (sorcery.revealUntilCreature)
            rules += "Reveal cards from the top of your library until you reveal a creature card. Put that card into your hand, then shuffle.\n";
        if (sorcery.revealUntilLand)
            rules += "Reveal cards from the top of your library until you reveal a land card. Put that card into your hand, then shuffle.\n";
        if (sorcery.destroyTargetIfTypeMatches)
        {
            string destroyType = sorcery.requiredTargetType switch
            {
                SorceryCard.TargetType.Creature => "creature",
                SorceryCard.TargetType.Land => "land",
                SorceryCard.TargetType.Artifact => "non-creature artifact",
                SorceryCard.TargetType.Enchantment => "enchantment",
                _ => "permanent"
            };

            if (!string.IsNullOrEmpty(sorcery.requiredTargetColor))
                destroyType = $"{sorcery.requiredTargetColor} {destroyType}";

            rules += $"Destroy target {destroyType}.\n";
        }
        rules = rules.TrimEnd();
        if (!string.IsNullOrEmpty(sorcery.flavorText))
        {
            if (!string.IsNullOrEmpty(rules))
                rules += "\n";
            rules += $"<i>{sorcery.flavorText}</i>";
        }
        keywordText.text = rules.Trim();
    }



    public void EnableTargetingHighlight(bool enable)
    {
        if (highlightBorder != null)
            highlightBorder.SetActive(enable);
    }


    private Sprite GetIconForColor(string color)
    {
        return color switch
        {
            "White" => whiteManaSymbol,
            "Blue" => blueManaSymbol,
            "Black" => blackManaSymbol,
            "Red" => redManaSymbol,
            "Green" => greenManaSymbol,
            _ => null
        };
    }
}
