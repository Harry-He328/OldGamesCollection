using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    [RequireComponent(typeof(Image))]
    public sealed class CellView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image symbol;
        [SerializeField] private Image effect;
        [SerializeField] private Sprite coveredSprite;
        [SerializeField] private Sprite revealedSprite;
        [SerializeField] private Sprite flagSprite;
        [SerializeField] private Sprite mineSprite;
        [SerializeField] private Color coveredColor = new Color32(174, 199, 219, 255);
        [SerializeField] private Color revealedColor = new Color32(229, 237, 243, 255);
        [SerializeField] private CellData data;
        private MinesweeperGame game;
        private Image background;
        private bool hovered;
        private bool pressed;
        private bool platformTile;
        private bool usedMine;
        private Action<CellData, CellAction> act;
        public CellData Data => data;
        public bool IsConfigured => label != null;
        public Sprite CoveredSprite => coveredSprite;
        public Sprite FlagSprite => flagSprite;
        public Sprite MineSprite => mineSprite;

        private static readonly Color[] NumberColors =
        {
            Color.black, new Color32(20, 70, 190, 255), new Color32(20, 120, 45, 255),
            new Color32(190, 35, 35, 255), new Color32(70, 35, 130, 255),
            new Color32(130, 55, 25, 255), new Color32(0, 115, 130, 255),
            Color.black, Color.gray
        };

        public void Bind(MinesweeperGame owner, CellData cell, Action<CellData, CellAction> onAction)
        {
            game = owner;
            data = cell;
            act = onAction;
            background = GetComponent<Image>();
            label.raycastTarget = false;
            Render();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (game == null) return;
            if (eventData.button == PointerEventData.InputButton.Right)
                act(data, CellAction.ToggleFlag);
            else if (eventData.button == PointerEventData.InputButton.Left)
                act(data, data.IsRevealed ? CellAction.Chord : CellAction.Reveal);
        }

        public void MarkMineUsed() => usedMine = true;

        public void ShowPlatformTile(bool solid, bool isSource)
        {
            platformTile = true;
            background.raycastTarget = false;
            background.enabled = solid;
            background.sprite = coveredSprite;
            background.color = Color.white;
            if (symbol != null) symbol.enabled = false;
            if (effect != null) effect.enabled = false;
            if (isSource || solid || !data.IsRevealed) label.text = string.Empty;
            label.color = new Color32(134, 155, 174, 66);
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; Render(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; Render(); }
        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = eventData.button == PointerEventData.InputButton.Left;
            Render();
        }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; Render(); }

        public void Render()
        {
            if (game == null || platformTile) return;
            bool showMine = !usedMine && data.HasMine && (data.IsRevealed || game.State == GameState.Lost);
            bool wrongFlag = game.State == GameState.Lost && data.IsFlagged && !data.HasMine;
            bool finished = game.State == GameState.Won || game.State == GameState.Lost;
            bool showPressed = pressed && hovered && !finished && !data.IsFlagged;
            Sprite sprite = data.IsRevealed || showMine || showPressed ? revealedSprite : coveredSprite;
            Sprite symbolSprite = showMine ? mineSprite : data.IsFlagged ? flagSprite : null;
            background.sprite = sprite;
            background.color = sprite != null ? Color.white : data.IsRevealed ? revealedColor : coveredColor;
            if (symbol != null)
            {
                symbol.sprite = symbolSprite;
                symbol.enabled = symbolSprite != null;
            }
            if (effect != null)
            {
                effect.color = data.IsDetonated ? new Color(1, 0, 0, .4f)
                    : new Color(1, 1, 1, .15f);
                effect.enabled = data.IsDetonated || (hovered && !finished && !data.IsRevealed);
            }
            label.color = Color.black;
            if (usedMine) { label.text = "•"; label.color = new Color32(107, 118, 124, 255); }
            else if (wrongFlag) { label.text = "X"; label.color = Color.red; }
            else if (symbol != null && symbolSprite != null) label.text = string.Empty;
            else if (showMine) label.text = "*";
            else if (data.IsFlagged) label.text = "F";
            else if (data.IsRevealed && data.AdjacentMines > 0)
            {
                label.text = data.AdjacentMines.ToString();
                label.color = NumberColors[Mathf.Clamp(data.AdjacentMines, 0, 8)];
            }
            else label.text = string.Empty;
        }
    }
}
