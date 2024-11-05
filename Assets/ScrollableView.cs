using UnityEngine;
using System.Collections.Generic;

public class ScrollableView : MonoBehaviour
{
    // ラベルのリスト（最新200行を保持）
    private List<string> labels = new List<string>();
    public int MaxLabels = 200;
    // スクロールビューのサイズ
    public float scrollViewWidth = 400f;
    public float scrollViewHeight = 300f;
    public int fontSize = 20;
    public int lineSpacingTop = -5;
    public int lineSpacingBottom = -5;
    // 文字間隔（ピクセル単位）
    [SerializeField] private float characterSpacing = -3f; // 負の値で狭く、正の値で広く
    // 背景色
    [SerializeField] private Color backgroundColor = new Color(1f, 0f, 0f, 1f); // デバッグ用に完全不透明な赤に設定
    // 背景画像（オプション）
    [SerializeField] private Texture2D backgroundImage;
    // カスタムGUIStyle
    private GUIStyle labelStyle;
    private GUIStyle verticalScrollbar;
    private bool stylesInitialized = false;
    private Vector2 scrollPosition = Vector2.zero;
    

    // ラベルを追加し、最大数を超えた場合は古いラベルを削除
    public void AddLabel(string text)
    {
        labels.Add(text);
        if (labels.Count > MaxLabels)
            labels.RemoveAt(0); // 最初のラベル（古いもの）を削除
    }


    void OnGUI()
    {
        // スタイルの初期化を一度だけ行う
        if (!stylesInitialized)
        {
            InitializeStyles();
            stylesInitialized = true;
        }

        // スクロールビューを配置する位置とサイズを定義
        float padding = 30f; // 画面端からの余白
        Rect scrollViewRect = new Rect(Screen.width - scrollViewWidth - padding, padding, scrollViewWidth, scrollViewHeight);

        // スクロールビューを配置するエリアの開始
        GUILayout.BeginArea(scrollViewRect);

        // 背景を描画（スクロールビューの前に描画することで、スクロールビューの背後に表示）
        if (backgroundImage != null)
        {
            // 背景画像を描画
            GUI.DrawTexture(new Rect(0, 0, scrollViewWidth, scrollViewHeight), backgroundImage, ScaleMode.StretchToFill, true);
        }
        else
        {
            // 背景色を描画
            GUI.color = backgroundColor;
            GUI.Box(new Rect(0, 0, scrollViewWidth, scrollViewHeight), GUIContent.none);
            GUI.color = Color.white; // GUI.colorを元に戻す
        }

        // スクロールビューの開始（基本的なオーバーロードを使用）
        scrollPosition = GUILayout.BeginScrollView(
            scrollPosition,
            false, // alwaysShowHorizontal: 水平スクロールバーを常に表示しない
            true,  // alwaysShowVertical: 垂直スクロールバーを常に表示
            GUILayout.Width(scrollViewWidth),
            GUILayout.Height(scrollViewHeight)
        );

        // スクロールビュー内に表示するコンテンツ
        GUILayout.BeginVertical();
        foreach (var label in labels)
        {
            // カスタムラベルを描画
            GUILayout.Label(label, labelStyle);
        }
        GUILayout.EndVertical();

        // スクロールビューの終了
        GUILayout.EndScrollView();

        // エリアの終了
        GUILayout.EndArea();
    }

   

    // カスタムスタイルを初期化
    private void InitializeStyles()
    {
        // ラベル用のカスタムGUIStyleを初期化
        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = fontSize; // フォントサイズを設定
        labelStyle.normal.textColor = Color.white; // テキストカラーを白に設定（デバッグ用）

        // マージンとパディングを調整して行間を狭くする
        labelStyle.margin = new RectOffset(0, 0, lineSpacingTop, lineSpacingBottom); // 上下のマージンを設定
        labelStyle.padding = new RectOffset(0, 0, 0, 0); // パディングをゼロに設定

        // スクロールバーのスタイルをカスタマイズ
        verticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar);
        verticalScrollbar.fixedWidth = 20;
        verticalScrollbar.normal.background = MakeTex(2, 2, Color.gray); // スクロールバーの背景色をグレーに設定
    }

    // カスタムラベルを描画。各文字を個別に描画し、指定した間隔を適用（今回は使用していませんが、必要に応じて利用可能です）
    /// <param name="text">表示するテキスト</param>
    /// <param name="style">使用するGUIStyle</param>
    /// <param name="spacing">文字間隔（ピクセル単位）</param>
    private void DrawCustomLabel(string text, GUIStyle style, float spacing)
    {
        // ラベル全体のサイズを計算
        GUIContent content = new GUIContent(text);
        Vector2 totalSize = style.CalcSize(content);

        // ラベルを描画する領域を取得
        Rect rect = GUILayoutUtility.GetRect(totalSize.x, totalSize.y, style);

        float x = rect.x;
        float y = rect.y;

        foreach (char c in text)
        {
            string charStr = c.ToString();
            GUIContent charContent = new GUIContent(charStr);
            Vector2 charSize = style.CalcSize(charContent);

            // 各文字を描画
            GUI.Label(new Rect(x, y, charSize.x, charSize.y), charContent, style);

            // 次の文字の位置を計算
            x += charSize.x + spacing;
        }
    }

    // 単色のテクスチャを生成します。
    /// <param name="width">テクスチャの幅</param>
    /// <param name="height">テクスチャの高さ</param>
    /// <param name="col">色</param>
    /// <returns>生成されたテクスチャ</returns>
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
