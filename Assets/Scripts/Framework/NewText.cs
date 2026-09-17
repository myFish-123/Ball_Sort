using UnityEngine.UI;
using UnityEngine;
using System.Reflection;

public class NewText : Text
{
    #region 实现超框时再缩小字体，适配多语言
    /// <summary>
    /// 当前可见的文字行数
    /// </summary>
    public int VisibleLines { get; private set; }

    private static readonly FieldInfo FontDataField = typeof(Text).GetField(
        "m_FontData",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private int GetFitFontSize()
    {
        TextGenerationSettings settings = GetGenerationSettings(rectTransform.rect.size);
        settings.resizeTextForBestFit = false;

        if (!resizeTextForBestFit)
        {
            cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
            return fontSize;
        }

        int minSize = resizeTextMinSize;
        int txtLen = text.Length;
        int bestSize = minSize;
        int bestVisibleCount = -1;
        for (int i = resizeTextMaxSize; i >= minSize; --i)
        {
            settings.fontSize = i;
            cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
            if (cachedTextGenerator.characterCountVisible == txtLen)
                return i;

            if (cachedTextGenerator.characterCountVisible > bestVisibleCount)
            {
                bestVisibleCount = cachedTextGenerator.characterCountVisible;
                bestSize = i;
            }
        }

        return bestSize;
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        if (null == font) return;

        if (!resizeTextForBestFit || FontDataField == null)
        {
            base.OnPopulateMesh(toFill);
            VisibleLines = cachedTextGenerator.lineCount;
            return;
        }

        FontData fontData = FontDataField.GetValue(this) as FontData;
        if (fontData == null)
        {
            base.OnPopulateMesh(toFill);
            VisibleLines = cachedTextGenerator.lineCount;
            return;
        }

        int originalFontSize = fontData.fontSize;
        bool originalBestFit = fontData.bestFit;

        fontData.fontSize = GetFitFontSize();
        fontData.bestFit = false;

        try
        {
            base.OnPopulateMesh(toFill);
            VisibleLines = cachedTextGenerator.lineCount;
        }
        finally
        {
            fontData.fontSize = originalFontSize;
            fontData.bestFit = originalBestFit;
        }
    }
    #endregion
}
