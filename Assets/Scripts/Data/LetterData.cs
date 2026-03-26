using UnityEngine;

/// <summary>
/// Colour theme for the bubble sprites of a given number.
/// Must match the filenames: blue-1/2, green-1/2, pink-1/2, red-1/2, yellow-1/2.
/// </summary>
public enum BubbleColor { Blue, Green, Pink, Red, Yellow }

/// <summary>
/// ScriptableObject that describes one number in the game.
/// Create via: Right-click > Create > PopIt > Number Data
/// </summary>
[CreateAssetMenu(fileName = "Number_0", menuName = "PopIt/Number Data")]
public class LetterData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("The number this asset represents (e.g. 0-9).")]
    public int number;

    [Header("Visuals")]
    [Tooltip("The number sprite shown as the pop-it board background (e.g. 0.png).")]
    public Sprite numberSprite;

    [Tooltip("Which bubble colour set to use for this number.")]
    public BubbleColor bubbleColor;

    [Header("Layout")]
    [Tooltip("Bubble spawn positions for this number shape.")]
    public BubbleLayoutData bubbleLayout;
}
