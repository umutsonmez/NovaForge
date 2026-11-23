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
        public const string WinTitle = "level.win_title";
        public const string LoseTitle = "level.lose_title";
        public const string Next = "common.next";
        public const string Home = "common.home";
        public const string DoubleReward = "reward.double";
        public const string DailyTitle = "daily.title";
        public const string DailyDay = "daily.day";
        public const string DailyClaim = "daily.claim";
        public const string DailyNextIn = "daily.next_in";
        public const string ShopTitle = "shop.title";
        public const string ShopOwned = "shop.owned";
        public const string ShopRestore = "shop.restore";
        public const string NotEnough = "shop.not_enough";
        public const string PurchaseFailed = "shop.purchase_failed";
        public const string PurchasePending = "shop.purchase_pending";

        private static readonly string[] Keys =
        {
            Ok, Cancel, Yes, No, Close, Retry, SettingsTitle, Music, Sound, Haptics, Notifications, Language,
            PrivacyOptions, PrivacyPolicy, Terms, Support, ExitTitle, ExitMessage, NoConnection,
            WinTitle, LoseTitle, Next, Home, DoubleReward, DailyTitle, DailyDay, DailyClaim, DailyNextIn, ShopTitle, ShopOwned, ShopRestore, NotEnough, PurchaseFailed, PurchasePending,
        };

        // Same order as Keys.
        private static readonly Dictionary<string, string[]> Values = new Dictionary<string, string[]>
        {
            [Languages.English] = new[]
            {
                "OK", "Cancel", "Yes", "No", "Close", "Retry", "Settings", "Music", "Sound", "Vibration", "Notifications", "Language",
                "Privacy settings", "Privacy Policy", "Terms of Service", "Contact support", "Quit game?", "Do you want to quit the game?",
                "No internet connection.",
                "Level Complete!", "Level Failed", "Next", "Home", "x2 Reward", "Daily Reward", "Day {0}", "Claim", "Next reward in {0}", "Shop", "Owned", "Restore Purchases", "Not enough resources.", "Purchase failed. Please try again.", "Purchase is pending. You will get it once the payment completes.",
            },
            [Languages.Turkish] = new[]
            {
                "Tamam", "İptal", "Evet", "Hayır", "Kapat", "Tekrar dene", "Ayarlar", "Müzik", "Ses", "Titreşim", "Bildirimler", "Dil",
                "Gizlilik ayarları", "Gizlilik Politikası", "Kullanım Koşulları", "Destek", "Oyundan çık?", "Oyundan çıkmak istiyor musun?",
                "İnternet bağlantısı yok.",
                "Bölüm Tamamlandı!", "Bölüm Başarısız", "Sonraki", "Ana Menü", "x2 Ödül", "Günlük Ödül", "{0}. Gün", "Al", "Sonraki ödül: {0}", "Mağaza", "Sahipsin", "Satın Alımları Geri Yükle", "Yeterli kaynağın yok.", "Satın alma başarısız. Lütfen tekrar dene.", "Satın alma beklemede. Ödeme tamamlanınca alacaksın.",
            },
            [Languages.German] = new[]
            {
                "OK", "Abbrechen", "Ja", "Nein", "Schließen", "Erneut versuchen", "Einstellungen", "Musik", "Sound", "Vibration", "Benachrichtigungen", "Sprache",
                "Datenschutzeinstellungen", "Datenschutzerklärung", "Nutzungsbedingungen", "Support kontaktieren", "Spiel beenden?", "Möchtest du das Spiel beenden?",
                "Keine Internetverbindung.",
                "Level geschafft!", "Level fehlgeschlagen", "Weiter", "Start", "x2 Belohnung", "Tägliche Belohnung", "Tag {0}", "Abholen", "Nächste Belohnung in {0}", "Shop", "Im Besitz", "Käufe wiederherstellen", "Nicht genug Ressourcen.", "Kauf fehlgeschlagen. Bitte versuche es erneut.", "Kauf ausstehend. Du erhältst ihn, sobald die Zahlung abgeschlossen ist.",
            },
            [Languages.French] = new[]
            {
                "OK", "Annuler", "Oui", "Non", "Fermer", "Réessayer", "Paramètres", "Musique", "Son", "Vibration", "Notifications", "Langue",
                "Paramètres de confidentialité", "Politique de confidentialité", "Conditions d'utilisation", "Contacter le support", "Quitter le jeu ?", "Voulez-vous quitter le jeu ?",
                "Pas de connexion Internet.",
                "Niveau terminé !", "Niveau échoué", "Suivant", "Accueil", "Récompense x2", "Récompense quotidienne", "Jour {0}", "Récupérer", "Prochaine récompense dans {0}", "Boutique", "Possédé", "Restaurer les achats", "Ressources insuffisantes.", "L'achat a échoué. Veuillez réessayer.", "Achat en attente. Vous le recevrez une fois le paiement terminé.",
            },
            [Languages.Spanish] = new[]
            {
                "Aceptar", "Cancelar", "Sí", "No", "Cerrar", "Reintentar", "Ajustes", "Música", "Sonido", "Vibración", "Notificaciones", "Idioma",
                "Ajustes de privacidad", "Política de privacidad", "Términos del servicio", "Contactar con soporte", "¿Salir del juego?", "¿Quieres salir del juego?",
                "Sin conexión a Internet.",
                "¡Nivel completado!", "Nivel fallido", "Siguiente", "Inicio", "Recompensa x2", "Recompensa diaria", "Día {0}", "Reclamar", "Próxima recompensa en {0}", "Tienda", "Comprado", "Restaurar compras", "No tienes suficientes recursos.", "La compra ha fallado. Inténtalo de nuevo.", "Compra pendiente. La recibirás cuando se complete el pago.",
            },
            [Languages.Portuguese] = new[]
            {
                "OK", "Cancelar", "Sim", "Não", "Fechar", "Tentar novamente", "Configurações", "Música", "Som", "Vibração", "Notificações", "Idioma",
                "Configurações de privacidade", "Política de Privacidade", "Termos de Serviço", "Falar com o suporte", "Sair do jogo?", "Deseja sair do jogo?",
                "Sem conexão com a internet.",
                "Fase concluída!", "Fase falhou", "Próxima", "Início", "Recompensa x2", "Recompensa diária", "Dia {0}", "Coletar", "Próxima recompensa em {0}", "Loja", "Adquirido", "Restaurar compras", "Recursos insuficientes.", "A compra falhou. Tente novamente.", "Compra pendente. Você receberá quando o pagamento for concluído.",
            },
            [Languages.Japanese] = new[]
            {
                "OK", "キャンセル", "はい", "いいえ", "閉じる", "再試行", "設定", "音楽", "効果音", "バイブレーション", "通知", "言語",
                "プライバシー設定", "プライバシーポリシー", "利用規約", "サポートに連絡", "ゲームを終了しますか？", "ゲームを終了してもよろしいですか？",
                "インターネットに接続されていません。",
                "レベルクリア！", "レベル失敗", "次へ", "ホーム", "報酬2倍", "デイリー報酬", "{0}日目", "受け取る", "次の報酬まで {0}", "ショップ", "所持済み", "購入を復元", "リソースが足りません。", "購入に失敗しました。もう一度お試しください。", "購入は保留中です。支払いが完了すると受け取れます。",
            },
            [Languages.Korean] = new[]
            {
                "확인", "취소", "예", "아니요", "닫기", "다시 시도", "설정", "음악", "효과음", "진동", "알림", "언어",
                "개인정보 설정", "개인정보 처리방침", "서비스 약관", "고객 지원", "게임을 종료할까요?", "게임을 종료하시겠습니까?",
                "인터넷에 연결되어 있지 않습니다.",
                "레벨 완료!", "레벨 실패", "다음", "홈", "보상 x2", "일일 보상", "{0}일차", "받기", "다음 보상까지 {0}", "상점", "보유 중", "구매 복원", "자원이 부족합니다.", "구매에 실패했습니다. 다시 시도해 주세요.", "구매 대기 중입니다. 결제가 완료되면 받게 됩니다.",
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
