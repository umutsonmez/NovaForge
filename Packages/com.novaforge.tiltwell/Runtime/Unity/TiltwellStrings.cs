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
        public const string ColorGuideTitle = "tiltwell.color_guide_title";
        public const string ColorGuideText = "tiltwell.color_guide_text";
        public const string GotIt = "tiltwell.got_it";
        public const string TutorialSticky = "tiltwell.tutorial_sticky";
        public const string TutorialArrow = "tiltwell.tutorial_arrow";
        public const string TutorialPaint = "tiltwell.tutorial_paint";
        public const string DailyPuzzle = "tiltwell.daily_puzzle";
        public const string DailyDoneTitle = "tiltwell.daily_done_title";
        public const string DailyDoneText = "tiltwell.daily_done_text";
        public const string DailyComeBack = "tiltwell.daily_come_back";

        private static readonly string[] Keys =
        {
            Title, Play, PlayLevel, Levels, LevelN, Moves, Undo, Restart, Hint, Stuck, NoHints, OutOfMovesTitle,
            ExtraMovesOffer, Tutorial, TutorialMix, AllDone,
            "resource.coin", "resource.hint", "shop.hints_3", "shop.hints_10",
            WorldN, TutorialBrown, ColorGuideTitle, ColorGuideText, GotIt,
            TutorialSticky, TutorialArrow, TutorialPaint,
            DailyPuzzle, DailyDoneTitle, DailyDoneText, DailyComeBack,
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
                "Mixing colors", "When two drops bump into each other, they mix:", "Got it!",
                "Honey tiles are sticky: a drop stops as soon as it slides onto one.",
                "Arrow tiles are one-way: drops can only cross them in the arrow's direction.",
                "A drop that slides over a paint tile picks up its color.",
                "Puzzle", "Daily puzzle solved!", "+{0} coins · Streak: {1} days", "You solved today's puzzle. A new one arrives tomorrow.",
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
                "Renkleri karıştır", "İki damla çarpışınca renkleri karışır:", "Anladım!",
                "Bal rengi kareler yapışkan: üstüne kayan damla orada durur.",
                "Oklu karelerden sadece ok yönünde geçilebilir.",
                "Boya karosunun üstünden geçen damla o rengi alır.",
                "Bulmaca", "Günün bulmacası çözüldü!", "+{0} altın · Seri: {1} gün", "Bugünün bulmacasını çözdün. Yenisi yarın gelir.",
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
                "Farben mischen", "Wenn zwei Tropfen zusammenstoßen, mischen sie sich:", "Verstanden!",
                "Honigfelder sind klebrig: Ein Tropfen bleibt darauf sofort stehen.",
                "Pfeilfelder sind Einbahnstraßen: Tropfen kommen nur in Pfeilrichtung durch.",
                "Ein Tropfen, der über ein Farbfeld rutscht, nimmt dessen Farbe an.",
                "Rätsel", "Tagesrätsel gelöst!", "+{0} Münzen · Serie: {1} Tage", "Du hast das heutige Rätsel gelöst. Morgen kommt ein neues.",
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
                "Mélanger les couleurs", "Quand deux gouttes se touchent, elles se mélangent :", "Compris !",
                "Les cases de miel collent : une goutte s'y arrête aussitôt.",
                "Les cases fléchées sont à sens unique : on ne les traverse que dans le sens de la flèche.",
                "Une goutte qui glisse sur une case de peinture prend sa couleur.",
                "Énigme", "Énigme du jour résolue !", "+{0} pièces · Série : {1} jours", "Vous avez résolu l'énigme du jour. Une nouvelle arrive demain.",
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
                "Mezclar colores", "Cuando dos gotas chocan, se mezclan:", "¡Entendido!",
                "Las casillas de miel son pegajosas: una gota se detiene en cuanto entra.",
                "Las casillas con flecha son de un solo sentido: solo se cruzan en la dirección de la flecha.",
                "Una gota que pasa sobre una casilla de pintura toma su color.",
                "Puzle", "¡Puzle del día resuelto!", "+{0} monedas · Racha: {1} días", "Ya resolviste el puzle de hoy. Mañana llega otro.",
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
                "Misturar cores", "Quando duas gotas se encontram, elas se misturam:", "Entendi!",
                "As casas de mel são grudentas: a gota para assim que entra nelas.",
                "As casas com seta são de mão única: só dá para passar na direção da seta.",
                "Uma gota que passa sobre uma casa de tinta pega a cor dela.",
                "Desafio", "Desafio do dia resolvido!", "+{0} moedas · Sequência: {1} dias", "Você resolveu o desafio de hoje. Amanhã tem outro.",
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
                "色を混ぜよう", "しずくがぶつかると色が混ざります：", "わかった！",
                "はちみつのマスはベタベタ。しずくは乗った瞬間に止まります。",
                "矢印のマスは一方通行。矢印の向きにしか通れません。",
                "ペンキのマスを通ったしずくはその色に染まります。",
                "パズル", "今日のパズルクリア！", "+{0}コイン · 連続{1}日", "今日のパズルはクリア済み。明日また新しいパズルが届きます。",
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
                "색 섞기", "물방울이 부딪히면 색이 섞입니다:", "알겠어요!",
                "꿀 칸은 끈적해요. 물방울이 올라가면 바로 멈춥니다.",
                "화살표 칸은 일방통행이에요. 화살표 방향으로만 지나갈 수 있어요.",
                "물감 칸을 지나간 물방울은 그 색을 띱니다.",
                "퍼즐", "오늘의 퍼즐 완료!", "+{0} 코인 · 연속 {1}일", "오늘의 퍼즐을 풀었어요. 내일 새 퍼즐이 나와요.",
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
