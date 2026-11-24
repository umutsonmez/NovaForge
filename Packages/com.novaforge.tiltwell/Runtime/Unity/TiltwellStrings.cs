using System.Collections.Generic;
using NovaForge.Core.Localization;

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>Game strings in the 8 supported languages (Core strings are loaded separately).</summary>
    public static class TiltwellStrings
    {
        public const string Title = "tiltwell.title";
        public const string Play = "tiltwell.play";
        public const string PlayLevel = "tiltwell.play_level";
        public const string Levels = "tiltwell.levels";
        public const string LevelN = "tiltwell.level_n";
        public const string Moves = "tiltwell.moves";
        public const string Undo = "tiltwell.undo";
        public const string Restart = "tiltwell.restart";
        public const string Hint = "tiltwell.hint";
        public const string Stuck = "tiltwell.stuck";
        public const string NoHints = "tiltwell.no_hints";
        public const string OutOfMovesTitle = "tiltwell.out_of_moves";
        public const string ExtraMovesOffer = "tiltwell.extra_moves_offer";
        public const string Tutorial = "tiltwell.tutorial";
        public const string TutorialMix = "tiltwell.tutorial_mix";
        public const string AllDone = "tiltwell.all_done";
        public const string WorldN = "tiltwell.world_n";
        public const string TutorialBrown = "tiltwell.tutorial_brown";

        private static readonly string[] Keys =
        {
            Title, Play, PlayLevel, Levels, LevelN, Moves, Undo, Restart, Hint, Stuck, NoHints, OutOfMovesTitle,
            ExtraMovesOffer, Tutorial, TutorialMix, AllDone,
            "resource.coin", "resource.hint", "shop.hints_3", "shop.hints_10",
            WorldN, TutorialBrown,
        };

        private static readonly Dictionary<string, string[]> Values = new Dictionary<string, string[]>
        {
            [Languages.English] = new[]
            {
                "Tiltwell", "Play", "Play · Level {0}", "Levels", "Level {0}", "Moves {0}/{1}", "Undo", "Restart", "Hint",
                "No way to finish from here. Undo or restart.", "No hints left.", "Out of moves",
                "Watch a short video for {0} extra moves?",
                "Swipe to tilt the board. Every drop slides until it hits a wall.",
                "Drops that collide mix their colors. Fill each well with its color.",
                "You finished every level!",
                "Coins", "Hints", "3 Hints", "10 Hints",
                "World {0}",
                "Brown wells need all three colors: red, yellow and blue.",
            },
            [Languages.Turkish] = new[]
            {
                "Tiltwell", "Oyna", "Oyna · Bölüm {0}", "Bölümler", "Bölüm {0}", "Hamle {0}/{1}", "Geri al", "Baştan", "İpucu",
                "Buradan bitirmek mümkün değil. Geri al ya da baştan başla.", "İpucun kalmadı.", "Hamlen bitti",
                "{0} ek hamle için kısa bir video izlemek ister misin?",
                "Tahtayı eğmek için kaydır. Her damla duvara çarpana kadar kayar.",
                "Çarpışan damlaların renkleri karışır. Her kuyuyu kendi rengiyle doldur.",
                "Bütün bölümleri bitirdin!",
                "Altın", "İpucu", "3 İpucu", "10 İpucu",
                "Dünya {0}",
                "Kahverengi kuyu üç rengin hepsini ister: kırmızı, sarı ve mavi.",
            },
            [Languages.German] = new[]
            {
                "Tiltwell", "Spielen", "Spielen · Level {0}", "Level", "Level {0}", "Züge {0}/{1}", "Zurück", "Neustart", "Tipp",
                "Von hier aus ist es nicht lösbar. Mach einen Zug rückgängig oder starte neu.", "Keine Tipps mehr.", "Keine Züge mehr",
                "Kurzes Video ansehen für {0} zusätzliche Züge?",
                "Wische, um das Brett zu kippen. Jeder Tropfen rutscht bis zur Wand.",
                "Tropfen, die zusammenstoßen, mischen ihre Farben. Fülle jeden Brunnen mit seiner Farbe.",
                "Du hast alle Level geschafft!",
                "Münzen", "Tipps", "3 Tipps", "10 Tipps",
                "Welt {0}",
                "Braune Brunnen brauchen alle drei Farben: Rot, Gelb und Blau.",
            },
            [Languages.French] = new[]
            {
                "Tiltwell", "Jouer", "Jouer · Niveau {0}", "Niveaux", "Niveau {0}", "Coups {0}/{1}", "Annuler", "Recommencer", "Indice",
                "Impossible de finir d'ici. Annulez ou recommencez.", "Plus d'indices.", "Plus de coups",
                "Regarder une courte vidéo pour {0} coups supplémentaires ?",
                "Glissez pour incliner le plateau. Chaque goutte glisse jusqu'au mur.",
                "Les gouttes qui se rencontrent mélangent leurs couleurs. Remplissez chaque puits de sa couleur.",
                "Vous avez terminé tous les niveaux !",
                "Pièces", "Indices", "3 indices", "10 indices",
                "Monde {0}",
                "Les puits bruns demandent les trois couleurs : rouge, jaune et bleu.",
            },
            [Languages.Spanish] = new[]
            {
                "Tiltwell", "Jugar", "Jugar · Nivel {0}", "Niveles", "Nivel {0}", "Movimientos {0}/{1}", "Deshacer", "Reiniciar", "Pista",
                "No se puede terminar desde aquí. Deshaz o reinicia.", "No te quedan pistas.", "Sin movimientos",
                "¿Ver un vídeo corto para {0} movimientos extra?",
                "Desliza para inclinar el tablero. Cada gota se desliza hasta chocar con una pared.",
                "Las gotas que chocan mezclan sus colores. Llena cada pozo con su color.",
                "¡Has completado todos los niveles!",
                "Monedas", "Pistas", "3 pistas", "10 pistas",
                "Mundo {0}",
                "Los pozos marrones necesitan los tres colores: rojo, amarillo y azul.",
            },
            [Languages.Portuguese] = new[]
            {
                "Tiltwell", "Jogar", "Jogar · Fase {0}", "Fases", "Fase {0}", "Movimentos {0}/{1}", "Desfazer", "Reiniciar", "Dica",
                "Não dá para terminar daqui. Desfaça ou reinicie.", "Você não tem mais dicas.", "Sem movimentos",
                "Assistir a um vídeo curto para ganhar {0} movimentos extras?",
                "Deslize para inclinar o tabuleiro. Cada gota desliza até bater na parede.",
                "Gotas que se encontram misturam as cores. Encha cada poço com a sua cor.",
                "Você concluiu todas as fases!",
                "Moedas", "Dicas", "3 dicas", "10 dicas",
                "Mundo {0}",
                "Poços marrons precisam das três cores: vermelho, amarelo e azul.",
            },
            [Languages.Japanese] = new[]
            {
                "Tiltwell", "プレイ", "プレイ · レベル{0}", "レベル", "レベル{0}", "手数 {0}/{1}", "戻す", "やり直す", "ヒント",
                "ここからはクリアできません。戻すかやり直してください。", "ヒントがありません。", "手数切れ",
                "短い動画を見て{0}手追加しますか？",
                "スワイプでボードを傾けよう。しずくは壁に当たるまですべります。",
                "ぶつかったしずくは色が混ざります。それぞれの井戸を同じ色で満たそう。",
                "全レベルクリア！",
                "コイン", "ヒント", "ヒント3個", "ヒント10個",
                "ワールド {0}",
                "茶色の井戸には赤・黄・青の3色すべてが必要です。",
            },
            [Languages.Korean] = new[]
            {
                "Tiltwell", "플레이", "플레이 · 레벨 {0}", "레벨", "레벨 {0}", "이동 {0}/{1}", "되돌리기", "다시 시작", "힌트",
                "여기서는 끝낼 수 없습니다. 되돌리거나 다시 시작하세요.", "힌트가 없습니다.", "이동 횟수 소진",
                "짧은 영상을 보고 {0}번 더 이동하시겠습니까?",
                "밀어서 보드를 기울이세요. 물방울은 벽에 닿을 때까지 미끄러집니다.",
                "부딪힌 물방울은 색이 섞입니다. 각 우물을 같은 색으로 채우세요.",
                "모든 레벨을 완료했습니다!",
                "코인", "힌트", "힌트 3개", "힌트 10개",
                "월드 {0}",
                "갈색 우물에는 빨강, 노랑, 파랑 세 가지 색이 모두 필요합니다.",
            },
        };

        public static IEnumerable<KeyValuePair<string, string>> AsJsonTables()
        {
            foreach (var lang in Values)
            {
                var obj = new Newtonsoft.Json.Linq.JObject();
                for (int i = 0; i < Keys.Length; i++) obj[Keys[i]] = lang.Value[i];
                yield return new KeyValuePair<string, string>(lang.Key, obj.ToString(Newtonsoft.Json.Formatting.None));
            }
        }

        internal static int KeyCount => Keys.Length;
        internal static IEnumerable<string[]> AllValues => Values.Values;
    }
}
