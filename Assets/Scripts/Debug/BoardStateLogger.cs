using UnityEngine;
using ImmersiveMapInterface.Board;

namespace ImmersiveMapInterface.Debugging
{
    /// <summary>
    /// Simple logger to verify PoleBasedBoardState change events.
    /// Attach to the GameObject that also holds the PoleBasedBoardState (e.g., Ground).
    /// </summary>
    public class BoardStateLogger : MonoBehaviour
    {
        [SerializeField] private PoleBasedBoardState boardState;
        [SerializeField] private bool logResets = true;
        [SerializeField] private bool logPieceChanges = true;

        private void Reset()
        {
            if (boardState == null) boardState = GetComponent<PoleBasedBoardState>();
        }

        private void OnEnable()
        {
            if (boardState == null) return;
            boardState.OnPieceChanged += HandlePieceChanged;
            boardState.OnBoardReset += HandleBoardReset;
        }

        private void OnDisable()
        {
            if (boardState == null) return;
            boardState.OnPieceChanged -= HandlePieceChanged;
            boardState.OnBoardReset -= HandleBoardReset;
        }

        private void HandlePieceChanged(int pole, int slot, PoleBasedBoardState.PieceColor color)
        {
            if (!logPieceChanges) return;
            Debug.Log($"BoardStateLogger: Pole {pole}, Slot {slot} => {color}", this);
        }

        private void HandleBoardReset()
        {
            if (!logResets) return;
            Debug.Log("BoardStateLogger: Board reset.", this);
        }
    }
}
