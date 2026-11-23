using System.Collections.Generic;

namespace NovaForge.Core.Localization
{
    /// <summary>
    /// Strings used by Core-provided UI (confirm popup, settings, exit dialog) in all 8 supported languages.
    /// Loaded before game tables, so a game can override any entry with the same key.
    /// </summary>
    public static class CoreStrings
    {
        public const string Ok = "common.ok";
        public const string Cancel = "common.cancel";
        public const string Yes = "common.yes";
        public const string No = "common.no";
        public const string Close = "common.close";
        public const string Retry = "common.retry";
        public const string SettingsTitle = "settings.title";
        public const string Music = "settings.music";
        public const string Sound = "settings.sound";
        public const string Haptics = "settings.haptics";
        public const string Notifications = "settings.notifications";
        public const string Language = "settings.language";
        public const string PrivacyOptions = "settings.privacy_options";
        public const string PrivacyPolicy = "settings.privacy_policy";
        public const string Terms = "settings.terms";
        public const string Support = "settings.support";
        public const string ExitTitle = "exit.title";
        public const string ExitMessage = "exit.message";
        public const string NoConnection = "error.no_connection";

        private static readonly string[] Keys =
        {
            Ok, Cancel, Yes, No, Close, Retry, SettingsTitle, Music, Sound, Haptics, Notifications, Language,
            PrivacyOptions, PrivacyPolicy, Terms, Support, ExitTitle, ExitMessage, NoConnection,
        };

        // Same order as Keys.
        private static readonly Dictionary<string, string[]> Values = new Dictionary<string, string[]>
        {
            [Languages.English] = new[]
            {
                "OK", "Cancel", "Yes", "No", "Close", "Retry", "Settings", "Music", "Sound", "Vibration", "Notifications", "Language",
                "Privacy settings", "Privacy Policy", "Terms of Service", "Contact support", "Quit game?", "Do you want to quit the game?",
                "No internet connection.",
            },
            [Languages.Turkish] = new[]
            {
                "Tamam", "İptal", "Evet", "Hayır", "Kapat", "Tekrar dene", "Ayarlar", "Müzik", "Ses", "Titreşim", "Bildirimler", "Dil",
                "Gizlilik ayarları", "Gizlilik Politikası", "Kullanım Koşulları", "Destek", "Oyundan çık?", "Oyundan çıkmak istiyor musun?",
                "İnternet bağlantısı yok.",
            },
            [Languages.German] = new[]
            {
                "OK", "Abbrechen", "Ja", "Nein", "Schließen", "Erneut versuchen", "Einstellungen", "Musik", "Sound", "Vibration", "Benachrichtigungen", "Sprache",
                "Datenschutzeinstellungen", "Datenschutzerklärung", "Nutzungsbedingungen", "Support kontaktieren", "Spiel beenden?", "Möchtest du das Spiel beenden?",
                "Keine Internetverbindung.",
            },
            [Languages.French] = new[]
            {
                "OK", "Annuler", "Oui", "Non", "Fermer", "Réessayer", "Paramètres", "Musique", "Son", "Vibration", "Notifications", "Langue",
                "Paramètres de confidentialité", "Politique de confidentialité", "Conditions d'utilisation", "Contacter le support", "Quitter le jeu ?", "Voulez-vous quitter le jeu ?",
                "Pas de connexion Internet.",
            },
            [Languages.Spanish] = new[]
            {
                "Aceptar", "Cancelar", "Sí", "No", "Cerrar", "Reintentar", "Ajustes", "Música", "Sonido", "Vibración", "Notificaciones", "Idioma",
                "Ajustes de privacidad", "Política de privacidad", "Términos del servicio", "Contactar con soporte", "¿Salir del juego?", "¿Quieres salir del juego?",
                "Sin conexión a Internet.",
            },
            [Languages.Portuguese] = new[]
            {
                "OK", "Cancelar", "Sim", "Não", "Fechar", "Tentar novamente", "Configurações", "Música", "Som", "Vibração", "Notificações", "Idioma",
                "Configurações de privacidade", "Política de Privacidade", "Termos de Serviço", "Falar com o suporte", "Sair do jogo?", "Deseja sair do jogo?",
                "Sem conexão com a internet.",
            },
            [Languages.Japanese] = new[]
            {
                "OK", "キャンセル", "はい", "いいえ", "閉じる", "再試行", "設定", "音楽", "効果音", "バイブレーション", "通知", "言語",
                "プライバシー設定", "プライバシーポリシー", "利用規約", "サポートに連絡", "ゲームを終了しますか？", "ゲームを終了してもよろしいですか？",
                "インターネットに接続されていません。",
            },
            [Languages.Korean] = new[]
            {
                "확인", "취소", "예", "아니요", "닫기", "다시 시도", "설정", "음악", "효과음", "진동", "알림", "언어",
                "개인정보 설정", "개인정보 처리방침", "서비스 약관", "고객 지원", "게임을 종료할까요?", "게임을 종료하시겠습니까?",
                "인터넷에 연결되어 있지 않습니다.",
            },
        };

        public static void AddTo(LocalizationService localization)
        {
            foreach (var lang in Values)
            {
                var table = new Dictionary<string, string>();
                for (int i = 0; i < Keys.Length; i++) table[Keys[i]] = lang.Value[i];
                localization.AddTable(lang.Key, table);
            }
        }
    }
}
