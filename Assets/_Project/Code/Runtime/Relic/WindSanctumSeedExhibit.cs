using MoreMountains.InventoryEngine;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace Mythos.Unleashed.Runtime.Relic
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Mythos/Relics/Wind Sanctum Seed Exhibit")]
    public sealed class WindSanctumSeedExhibit : ButtonActivated
    {
        [Header("Authentic Relic")]
        [SerializeField]
        private InventoryItem windSanctumSeed;

        [SerializeField]
        private GameObject displayedSeed;

        [Header("Interaction Text")]
        [SerializeField]
        private string depositPrompt = "Place Wind Sanctum Seed";

        [SerializeField]
        private string retrievePrompt = "Retrieve Wind Sanctum Seed";

        private CharacterInventory _interactingCharacterInventory;
        private bool _transactionInProgress;

        public override void Initialization()
        {
            base.Initialization();
            RefreshPresentation();
        }

        public override void TriggerEnterAction(GameObject collider)
        {
            base.TriggerEnterAction(collider);

            Character character = collider != null
                ? collider.GetComponent<Character>()
                : null;

            _interactingCharacterInventory =
                character != null ? character.FindAbility<CharacterInventory>() : null;

            RefreshPrompt();
        }

        public override void TriggerExitAction(GameObject collider)
        {
            base.TriggerExitAction(collider);
            _interactingCharacterInventory = null;
        }

        public override void TriggerButtonAction()
        {
            if (_transactionInProgress || !CheckNumberOfUses())
            {
                PromptError();
                return;
            }

            GameState gameState = GameState.Instance;
            Inventory inventory = GetAuthoritativeInventory();

            if (gameState == null || inventory == null || windSanctumSeed == null)
            {
                PromptError();
                return;
            }

            bool succeeded;
            _transactionInProgress = true;

            try
            {
                succeeded = gameState.WindSanctumSeedOnDisplay
                    ? TryRetrieve(inventory, gameState)
                    : TryDeposit(inventory, gameState);
            }
            finally
            {
                _transactionInProgress = false;
                RefreshPresentation();
            }

            if (!succeeded)
            {
                PromptError();
                return;
            }

            base.TriggerButtonAction();
        }

        public override void ShowPrompt()
        {
            base.ShowPrompt();

            // The native prompt is authored for a single input glyph. Keep its
            // lifecycle and styling, but give this action label readable room.
            if (_buttonPrompt == null ||
                _buttonPrompt.PromptText == null ||
                _buttonPrompt.Border == null ||
                _buttonPrompt.Background == null)
            {
                return;
            }

            _buttonPrompt.PromptText.resizeTextForBestFit = true;
            _buttonPrompt.PromptText.resizeTextMinSize = 36;
            _buttonPrompt.PromptText.resizeTextMaxSize = 160;
            _buttonPrompt.PromptText.rectTransform.sizeDelta =
                new Vector2(1800f, 300f);

            _buttonPrompt.Border.rectTransform.sizeDelta =
                new Vector2(220f, 100f);
            _buttonPrompt.Background.rectTransform.sizeDelta =
                new Vector2(200f, 90f);
        }

        private bool TryDeposit(Inventory inventory, GameState gameState)
        {
            int quantityBefore = inventory.GetQuantity(windSanctumSeed.ItemID);
            if (quantityBefore < 1)
            {
                return false;
            }

            bool removed = inventory.RemoveItemByID(windSanctumSeed.ItemID, 1);
            int quantityAfter = inventory.GetQuantity(windSanctumSeed.ItemID);

            if (!removed || quantityAfter != quantityBefore - 1)
            {
                RestoreQuantityIfNeeded(inventory, quantityBefore, quantityAfter);
                return false;
            }

            if (gameState.TryPlaceWindSanctumSeedOnDisplay())
            {
                return true;
            }

            bool restored = inventory.AddItem(windSanctumSeed, 1) &&
                            inventory.GetQuantity(windSanctumSeed.ItemID) == quantityBefore;

            if (!restored)
            {
                Debug.LogError(
                    "[WindSanctumSeedExhibit] Deposit rollback failed.",
                    this
                );
            }

            return false;
        }

        private bool TryRetrieve(Inventory inventory, GameState gameState)
        {
            if (inventory.CapMaxQuantity(windSanctumSeed, 1) < 1 ||
                inventory.NumberOfStackableSlots(
                    windSanctumSeed.ItemID,
                    windSanctumSeed.MaximumStack
                ) < 1)
            {
                return false;
            }

            int quantityBefore = inventory.GetQuantity(windSanctumSeed.ItemID);
            bool added = inventory.AddItem(windSanctumSeed, 1);
            int quantityAfter = inventory.GetQuantity(windSanctumSeed.ItemID);

            if (!added || quantityAfter != quantityBefore + 1)
            {
                int excessQuantity = quantityAfter - quantityBefore;
                if (excessQuantity > 0)
                {
                    bool failedAddRolledBack = inventory.RemoveItemByID(
                        windSanctumSeed.ItemID,
                        excessQuantity
                    ) && inventory.GetQuantity(windSanctumSeed.ItemID) == quantityBefore;

                    if (!failedAddRolledBack)
                    {
                        Debug.LogError(
                            "[WindSanctumSeedExhibit] Failed retrieval rollback failed.",
                            this
                        );
                    }
                }

                return false;
            }

            if (gameState.TryRemoveWindSanctumSeedFromDisplay())
            {
                return true;
            }

            bool rolledBack = inventory.RemoveItemByID(windSanctumSeed.ItemID, 1) &&
                              inventory.GetQuantity(windSanctumSeed.ItemID) == quantityBefore;

            if (!rolledBack)
            {
                Debug.LogError(
                    "[WindSanctumSeedExhibit] Retrieval rollback failed.",
                    this
                );
            }

            return false;
        }

        private void RestoreQuantityIfNeeded(
            Inventory inventory,
            int expectedQuantity,
            int currentQuantity
        )
        {
            int missingQuantity = expectedQuantity - currentQuantity;
            if (missingQuantity <= 0)
            {
                return;
            }

            bool restored = inventory.AddItem(windSanctumSeed, missingQuantity) &&
                            inventory.GetQuantity(windSanctumSeed.ItemID) == expectedQuantity;

            if (!restored)
            {
                Debug.LogError(
                    "[WindSanctumSeedExhibit] Inventory removal rollback failed.",
                    this
                );
            }
        }

        private Inventory GetAuthoritativeInventory()
        {
            if (_interactingCharacterInventory == null)
            {
                return null;
            }

            Inventory inventory = _interactingCharacterInventory.MainInventory;
            if (inventory != null &&
                inventory.name == _interactingCharacterInventory.MainInventoryName &&
                inventory.PlayerID == _interactingCharacterInventory.PlayerID)
            {
                return inventory;
            }

            if (Inventory.RegisteredInventories == null)
            {
                return null;
            }

            return Inventory.FindInventory(
                _interactingCharacterInventory.MainInventoryName,
                _interactingCharacterInventory.PlayerID
            );
        }

        private void RefreshPresentation()
        {
            bool seedIsDisplayed = GameState.Instance != null &&
                                   GameState.Instance.WindSanctumSeedOnDisplay;

            if (displayedSeed != null)
            {
                displayedSeed.SetActive(seedIsDisplayed);
            }

            RefreshPrompt();
        }

        private void RefreshPrompt()
        {
            ButtonPromptText = GameState.Instance != null &&
                               GameState.Instance.WindSanctumSeedOnDisplay
                ? retrievePrompt
                : depositPrompt;

            if (_interactingCharacterInventory != null)
            {
                HidePrompt();
                ShowPrompt();
            }
        }
    }
}
