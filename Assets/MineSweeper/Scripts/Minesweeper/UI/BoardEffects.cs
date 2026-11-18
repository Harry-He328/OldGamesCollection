using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    internal sealed class BoardEffects
    {
        private const float FlightDuration = .42f;
        private readonly RectTransform window;
        private readonly RectTransform board;
        private readonly RectTransform face;
        private readonly List<Flight> flights = new List<Flight>();
        private Image runner;
        private Image flag;
        private RectTransform platformLayer;

        private sealed class Flight
        {
            public Image Image;
            public RectTransform Source;
            public RectTransform Target;
            public bool HitsBoss;
            public float Age;
        }

        public BoardEffects(RectTransform window, RectTransform board, RectTransform face)
        {
            this.window = window;
            this.board = board;
            this.face = face;
        }

        public void ShowPlatform(PlatformRun run, CellView source)
        {
            var layer = new GameObject("Platform Actors", typeof(RectTransform),
                typeof(LayoutElement), typeof(RectMask2D));
            layer.layer = board.gameObject.layer;
            layer.GetComponent<LayoutElement>().ignoreLayout = true;
            platformLayer = (RectTransform)layer.transform;
            platformLayer.SetParent(board, false);
            platformLayer.anchorMin = Vector2.zero;
            platformLayer.anchorMax = Vector2.one;
            platformLayer.offsetMin = platformLayer.offsetMax = Vector2.zero;

            runner = CreateImage(platformLayer, "Runner", source.CoveredSprite, Color.white);
            flag = CreateImage(platformLayer, "Finish Flag", source.FlagSprite, Color.white);
            AddOutline(flag, new Color32(240, 182, 63, 255));
            Place(flag.rectTransform, run, run.Goal, new Vector2(.88f, .88f));
            PositionRunner(run);
        }

        private static void AddOutline(Image image, Color color)
        {
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        public void PositionRunner(PlatformRun run)
        {
            Place(runner.rectTransform, run, run.Position + Vector2.up * .5f, Vector2.one);
        }

        private static void Place(RectTransform rect, PlatformRun run, Vector2 center, Vector2 size)
        {
            var scale = new Vector2(1f / run.Width, 1f / run.BoardHeight);
            rect.anchorMin = Vector2.Scale(center - size * .5f, scale);
            rect.anchorMax = Vector2.Scale(center + size * .5f, scale);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void Launch(CellView cell)
        {
            var image = CreateImage(window, "Flying Mine", cell.MineSprite, Color.white);
            AddFlight(image, (RectTransform)cell.transform, face, true);
        }

        public void Counterattack(RectTransform player)
        {
            var image = CreateImage(window, "Boss Counterattack", null, new Color32(233, 61, 42, 255));
            AddFlight(image, face, player, false);
        }

        private void AddFlight(Image image, RectTransform source, RectTransform target, bool hitsBoss)
        {
            var flight = new Flight { Image = image, Source = source, Target = target, HitsBoss = hitsBoss };
            flights.Add(flight);
            PositionFlight(flight);
        }

        public bool AnimateFlights(float seconds)
        {
            bool impacted = false;
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight flight = flights[i];
                flight.Age += seconds;
                if (flight.Age >= FlightDuration)
                {
                    Remove(flight.Image.gameObject);
                    flights.RemoveAt(i);
                    impacted |= flight.HitsBoss;
                }
                else PositionFlight(flight);
            }
            return impacted;
        }

        private void PositionFlight(Flight flight)
        {
            float t = Mathf.Clamp01(flight.Age / FlightDuration);
            Vector2 from = window.InverseTransformPoint(flight.Source.TransformPoint(flight.Source.rect.center));
            Vector2 to = window.InverseTransformPoint(flight.Target.TransformPoint(flight.Target.rect.center));
            float arcHeight = flight.HitsBoss ? 38f : -32f;
            Vector2 position = Vector2.Lerp(from, to, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * arcHeight);
            flight.Image.rectTransform.localPosition = position;
            flight.Image.rectTransform.sizeDelta = Vector2.one *
                (flight.HitsBoss ? Mathf.Lerp(Mathf.Min(30, flight.Source.rect.width), 12, t) : 10);
            flight.Image.rectTransform.localRotation = Quaternion.Euler(0, 0, 45 - 180 * t);
        }

        public void Clear()
        {
            if (platformLayer != null)
            {
                Remove(platformLayer.gameObject);
                platformLayer = null;
            }
            foreach (Flight flight in flights) Remove(flight.Image.gameObject);
            flights.Clear();
        }

        private static void Remove(GameObject target)
        {
            target.SetActive(false);
            Object.Destroy(target);
        }

        private static Image CreateImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.layer = parent.gameObject.layer;
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
