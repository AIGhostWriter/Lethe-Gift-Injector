// LetheGiftInjector (ver.2.8.0)
// - 플레이스홀더 ID 이름 대량 업데이트 (공개 데이터 기반)
// - 미확인 항목은 (ID:XXXX) 유지

using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace LetheGiftInjector
{
    [BepInPlugin("com.mod.lethegiftinjector", "LetheGiftInjector", "2.8.0")]
    public class LetheGiftInjectorPlugin : BasePlugin
    {
        internal static new ManualLogSource? Log;
        internal static string TokenFilePath =>
            Path.Combine(Paths.ConfigPath, "LetheGiftInjector_token.txt");

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo("LetheGiftInjector v2.8.0 로드");
            AddComponent<InjectorUI>();
        }
    }

    public class InjectorUI : MonoBehaviour
    {
        private static readonly HttpClient _http = new HttpClient();
        private const string FETCH_URL  = "https://api.lethelc.site/dashboard/md/get";
        private const string UPDATE_URL = "https://api.lethelc.site/dashboard/md/update";

        private bool      _showPanel   = false;
        private string    _token       = "";
        private string    _status      = "";
        private JsonNode? _state       = null;
        private bool      _fetched     = false;
        private Task?     _pendingTask = null;
        private string    _egsKey      = "egs";

        private string _inputId   = "";
        private string _inputTier = "2";

        private int _giftPage = 0;
        private const int PAGE_SIZE = 8;

        private Rect    _windowRect  = new Rect(20, 20, 540, 640);
        private bool    _isDragging  = false;
        private Vector2 _dragOffset  = Vector2.zero;

        private System.Collections.Generic.List<(int id, int ul)> _pendingGifts
            = new System.Collections.Generic.List<(int id, int ul)>();

        private readonly string[] _kwRow1 = { "All","Combustion","Laceration","Vibration","Burst","Sinking" };
        private readonly string[] _kwRow2 = { "Breath","Charge","Slash","Penetrate","Hit","None" };
        private readonly string[] _allKeywords = {
            "All","Combustion","Laceration","Vibration","Burst","Sinking",
            "Breath","Charge","Slash","Penetrate","Hit","None"
        };
        private int _kwIndex = 0;

        private readonly (int gid, string name, string kw)[] _allGifts = {
            // ── 일반 기프트 9001–9154 ─────────────────────────────────────────
            (9001,"Hellterfly's Dream","Combustion"),
            (9002,"Perversion","None"),
            (9003,"Ashes to Ashes","Combustion"),
            (9004,"Phlebotomy Pack","None"),
            (9005,"Wound Clerid","Laceration"),
            (9006,"Coffee and Cranes","None"),
            (9007,"Eclipse of Scarlet Moths","None"),
            (9008,"Grimy Iron Stake","Laceration"),
            (9009,"Fiery Down","Combustion"),
            (9010,"Bloody Gadget","None"),
            (9011,"Sunshower","None"),
            (9012,"Today's Expression","Hit"),
            (9013,"Talisman Bundle","Burst"),
            (9014,"Rusty Commemorative Coin","None"),
            (9015,"Blood, Sweat, and Tears","None"),
            (9016,"Green Spirit","Vibration"),
            (9017,"Lithograph","None"),
            (9018,"Crown of Roses","Burst"),
            (9019,"Sticky Muck","Penetrate"),
            (9020,"White Gossypium","Laceration"),
            (9021,"Blue Zippo Lighter","None"),
            (9022,"Phantom Pain","None"),
            (9023,"Thunderbranch","Burst"),
            (9024,"Melted Eyeball","Vibration"),
            (9025,"Grey Coat","None"),
            (9026,"Late-bloomer's Tattoo","None"),
            (9027,"Lowest Star","Hit"),
            (9028,"Prejudice","None"),
            (9029,"Little and To-be-Naughty Plushie","Laceration"),
            (9030,"Gathering Skulls","Penetrate"),
            (9031,"Nixie Divergence","Vibration"),
            (9032,"Dreaming Electric Sheep","Slash"),
            (9033,"Standard-duty Battery","Burst"),
            (9034,"Pinpoint Logic Circuit","Combustion"),
            (9035,"Voodoo Doll","None"),
            (9036,"Carmilla","None"),
            (9037,"Child within a Flask","None"),
            (9038,"Illusory Hunt","None"),
            (9039,"Homeward","None"),
            (9040,"Tomorrow's Fortune","None"),
            (9041,"Red Order","Sinking"),
            (9042,"Smokes and Wires","Laceration"),
            (9043,"Employee Card","Charge"),
            (9044,"Oscillating Bracelet","Vibration"),
            (9045,"Glimpse of Flames","Combustion"),
            (9046,"Cigarette Holder","Breath"),
            (9047,"Barbed Lasso","Burst"),
            (9048,"Rusted Cutting Knife","Laceration"),
            (9049,"Thorny Path","Sinking"),
            (9050,"Red-stained Gossypium","Laceration"),
            (9051,"Stone Tomb","Breath"),
            (9052,"Portable Battery Socket","Charge"),
            (9053,"Dust to Dust","Combustion"),
            (9054,"Melted Spring","Sinking"),
            (9055,"Downpour","Vibration"),
            (9056,"Four-leaf Clover","Breath"),
            (9057,"Nightvision Goggles","Charge"),
            (9058,"Disk Fragment","None"),
            (9059,"Midwinter Nightmare","Sinking"),
            (9060,"Thrill","Burst"),
            (9061,"Skeletal Crumbs","Sinking"),
            (9062,"Curriculum Vitae","Charge"),
            (9063,"Pendant of Nostalgia","Breath"),
            (9064,"Broken Revolver","Burst"),
            (9065,"Artistic Sense","Sinking"),
            (9066,"Nebulizer","Breath"),
            (9067,"Special Contract","None"),
            (9068,"Grand Welcome","None"),
            (9069,"Wrist Guards","Charge"),
            (9070,"Clear Mirror, Calm Water","Breath"),
            (9071,"Charred Disk","Combustion"),
            (9072,"Lightning Rod","Charge"),
            (9073,"Endorphin Kit","Breath"),
            (9074,"Headless Portrait","Sinking"),
            (9075,"Charge-type Gloves","Charge"),
            (9076,"First-aid Kit","None"),
            (9077,"Painkiller","None"),
            (9078,"Voracious Hammer","None"),
            (9079,"Golden Urn","None"),
            (9080,"Milepost of Survival","None"),
            (9081,"Faith","None"),
            (9082,"Piece of Relationship","None"),
            (9083,"Lunar Memory","None"),
            (9084,"Ancient Effigy","None"),
            (9085,"Non-disclosure Agreement","None"),
            (9086,"Reverberation","Vibration"),
            (9087,"Burning Intellect","Combustion"),
            (9088,"Soothe the Dead","Combustion"),
            (9089,"Rusted Muzzle","Laceration"),
            (9090,"Bloody Mist","Laceration"),
            (9091,"Bell of Truth","Vibration"),
            (9092,"Coupled Oscillation","Vibration"),
            (9093,"Fluorescent Lamp","Burst"),
            (9094,"Enrapturing Trance","Burst"),
            (9095,"Broken Compass","Sinking"),
            (9096,"Black Sheet Music","Sinking"),
            (9097,"Ornamental Horseshoe","Breath"),
            (9098,"Lucky Pouch","Breath"),
            (9099,"Material Interference Force Field","Charge"),
            (9100,"T-1 Perpetual Motion Machine","Charge"),
            (9101,"Melted Paraffin","Combustion"),
            (9102,"Polarization","Combustion"),
            (9103,"Pain of Stifled Rage","Combustion"),
            (9104,"Ardent Flower","Combustion"),
            (9105,"Fragment of Hellfire","Combustion"),
            (9106,"Arrested Hymn","Laceration"),
            (9107,"Tangled Bundle","Laceration"),
            (9108,"Awe","Laceration"),
            (9109,"Respite","Laceration"),
            (9110,"Fragment of Allurement","Laceration"),
            (9111,"Bio-venom Vial","Vibration"),
            (9112,"Venomous Skin","Vibration"),
            (9113,"Sour Liquor Aroma","Vibration"),
            (9114,"Mirror Tactile Synaesthesia","Vibration"),
            (9115,"Clockwork Spring","Vibration"),
            (9116,"Fragment of Inertia","Vibration"),
            (9117,"Smoking Gunpowder","Burst"),
            (9118,"Bone Stake","Burst"),
            (9119,"Ragged Umbrella","Burst"),
            (9120,"Deathseeker","Burst"),
            (9121,"Fragment of Desire","Burst"),
            (9122,"Eldtree Snare","Sinking"),
            (9123,"Rags","Sinking"),
            (9124,"Grandeur","Sinking"),
            (9125,"Distant Star","Sinking"),
            (9126,"Fragment of Decay","Sinking"),
            (9127,"Devil's Share","Breath"),
            (9128,"Emerald Elytra","Breath"),
            (9129,"Old Wooden Doll","Breath"),
            (9130,"Finifugality","Breath"),
            (9131,"Fragment of Conceit","Breath"),
            (9132,"UPS System","Charge"),
            (9133,"Uncapped Defibrilator","Charge"),
            (9134,"Patrolling Flashlight","Charge"),
            (9135,"Imitative Generator","Charge"),
            (9136,"Fragment of Friction","Charge"),
            (9137,"Scalpel","Slash"),
            (9138,"Deceptive Accord","Slash"),
            (9139,"Tailor's Scissors","Slash"),
            (9140,"Resolution","Slash"),
            (9141,"Moment of Sentencing","Slash"),
            (9142,"Sundered Memory","Slash"),
            (9143,"Carpenter's Nail","Penetrate"),
            (9144,"Once, A Blessing","Penetrate"),
            (9145,"Torn Bandolier","Penetrate"),
            (9146,"Keenbranch","Penetrate"),
            (9147,"Punctured Memory","Penetrate"),
            (9148,"Burial Curse","Hit"),
            (9149,"Compression Bandage","Hit"),
            (9150,"Temporal Bridle","Hit"),
            (9151,"Clasped Sculpture","Hit"),
            (9152,"Crushed Memory","Hit"),
            (9153,"Oracle","None"),
            (9154,"Imposed Weight","None"),
            // ── 시즌별 추가 기프트 ─────────────────────────────────────────────
            (9155,"Decamillennial Stewpot","Combustion"),
            (9156,"Decamillennial Hearthflame","Combustion"),
            (9157,"Secret Cookbook","Combustion"),
            (9158,"Purloined Flame","Combustion"),
            (9159,"Millarca","Laceration"),
            (9160,"Ruptured Blood Sac","Laceration"),
            (9161,"Devotion","Laceration"),
            (9162,"Hemorrhagic Shock","Laceration"),
            (9163,"Gemstone Oscillator","Vibration"),
            (9164,"Wobbling Keg","Vibration"),
            (9165,"Interlocked Cogs","Vibration"),
            (9166,"Epicenter","Vibration"),
            (9167,"Omnivibro-octovecti-bell","Vibration"),
            (9168,"Shard of Apocalypse","Burst"),
            (9169,"Thorny Rope Cuffs","Burst"),
            (9170,"Eerie Effigy","Burst"),
            (9171,"Ruin","Burst"),
            (9172,"Cantabile","Sinking"),
            (9173,"Faded Overcoat","Sinking"),
            (9174,"Tangled Bones","Sinking"),
            (9175,"Surging Globe","Sinking"),
            (9176,"Impending Wave","Sinking"),
            (9177,"Recollection of a Certain Day","Breath"),
            (9178,"Angel's Cut","Breath"),
            (9179,"Reminiscence","Breath"),
            (9180,"Cask Spirits","Breath"),
            (9181,"Miniature Telepole","Charge"),
            (9182,"T-1B Octagonal Bolt","Charge"),
            (9183,"Insulator","Charge"),
            (9184,"T-5 Perpetual Motion Machine","Charge"),
            (9185,"Rebate Token","None"),
            (9186,"New Release Pamphlet","None"),
            (9187,"Special Catalogue","None"),
            (9188,"Pre-order Discount","None"),
            (9189,"Renewed Merch","None"),
            (9190,"Trial Plan Guide","None"),
            (9191,"Prestige Card","None"),
            (9192,"Layered Bandages","None"),
            (9193,"Overused Whetstone","Slash"),
            (9194,"Short Cane Sword","None"),
            (9195,"Cloudpattern Gourd Bottle","Slash"),
            (9196,"Broken Greatsword","Slash"),
            (9197,"High-tensility Shoes","None"),
            (9198,"Plume of Proof","None"),
            (9199,"Torn Hems","None"),
            (9200,"Dueling Manual Book 3","None"),
            (9201,"Dimensional Recycle Bin","Hit"),
            (9202,"Pocket Flashcards","None"),
            (9203,"Dimensional Perception Modifier","Hit"),
            (9204,"The Book of Vengeance","Hit"),
            (9205,"(ID:9205)","Laceration"),
            (9206,"(ID:9206)","Laceration"),
            (9207,"(ID:9207)","None"),
            (9208,"(ID:9208)","None"),
            (9209,"(ID:9209)","None"),
            (9210,"(ID:9210)","Hit"),
            (9211,"(ID:9211)","Sinking"),
            (9212,"(ID:9212)","Breath"),
            (9213,"(ID:9213)","Laceration"),
            (9214,"(ID:9214)","Vibration"),
            (9215,"(ID:9215)","Combustion"),
            (9216,"(ID:9216)","Combustion"),
            (9217,"(ID:9217)","Sinking"),
            (9218,"(ID:9218)","Breath"),
            (9219,"(ID:9219)","None"),
            (9220,"(ID:9220)","Hit"),
            (9221,"(ID:9221)","Laceration"),
            (9222,"(ID:9222)","Laceration"),
            (9223,"(ID:9223)","Laceration"),
            (9224,"(ID:9224)","Laceration"),
            (9225,"(ID:9225)","None"),
            (9226,"(ID:9226)","Breath"),
            (9227,"(ID:9227)","None"),
            (9228,"(ID:9228)","None"),
            (9229,"(ID:9229)","None"),
            (9230,"(ID:9230)","None"),
            (9231,"(ID:9231)","None"),
            (9232,"(ID:9232)","None"),
            (9233,"(ID:9233)","Sinking"),
            (9234,"(ID:9234)","Laceration"),
            (9235,"(ID:9235)","Penetrate"),
            (9236,"(ID:9236)","Laceration"),
            (9237,"(ID:9237)","Laceration"),
            (9238,"(ID:9238)","Sinking"),
            (9239,"(ID:9239)","Sinking"),
            (9240,"(ID:9240)","Combustion"),
            (9241,"(ID:9241)","None"),
            (9242,"(ID:9242)","None"),
            (9243,"(ID:9243)","Combustion"),
            (9244,"(ID:9244)","Burst"),
            (9245,"(ID:9245)","Charge"),
            (9246,"(ID:9246)","None"),
            (9247,"(ID:9247)","Combustion"),
            (9248,"(ID:9248)","Burst"),
            (9249,"(ID:9249)","None"),
            (9256,"(ID:9256)","Vibration"),
            (9257,"(ID:9257)","Sinking"),
            (9258,"(ID:9258)","Hit"),
            (9259,"(ID:9259)","Laceration"),
            (9260,"(ID:9260)","Charge"),
            (9261,"(ID:9261)","Sinking"),
            (9262,"(ID:9262)","None"),
            (9263,"(ID:9263)","Laceration"),
            (9264,"(ID:9264)","Laceration"),
            (9265,"(ID:9265)","Hit"),
            (9266,"(ID:9266)","None"),
            (9267,"(ID:9267)","Combustion"),
            (9268,"(ID:9268)","None"),
            (9403,"Ebony Brooch","Burst"),
            (9404,"Contained Maggots","Laceration"),
            (9407,"Made-to-Order","None"),
            (9408,"Haunted Shoes","None"),
            (9409,"Frozen Cries","Sinking"),
            (9410,"Hoarfrost Footprint","Sinking"),
            (9413,"Nagel und Hammer Scriptures","Laceration"),
            (9414,"Blood-red Mane","None"),
            (9415,"Squalidity","Laceration"),
            (9416,"Wholeness","Laceration"),
            (9419,"Spicebush Branch","None"),
            (9420,"Kaleidoscope","None"),
            (9423,"Broken Glasses","None"),
            (9424,"Unmailed Letter","Burst"),
            (9427,"(ID:9427)","None"),
            (9428,"(ID:9428)","Breath"),
            (9429,"Harpoon Prosthetic Leg","Breath"),
            (9430,"Guiding Gas Lamp","Breath"),
            (9431,"(ID:9431)","None"),
            (9432,"(ID:9432)","Sinking"),
            (9433,"Chief Butler's Secret Arts","None"),
            (9434,"Handheld Mirror","None"),
            (9435,"(ID:9435)","None"),
            (9436,"Refraction Glass Pod","Sinking"),
            (9437,"La Manchaland All-day Pass","Laceration"),
            (9438,"Token of Victory","Laceration"),
            (9439,"Devouring Cube","None"),
            (9440,"Mask of the Parade","Laceration"),
            (9701,"Hot 'n Juicy Drumstick","Combustion"),
            (9702,"Dry-to-the-Bone Breast","Burst"),
            (9703,"Tango Marinade","None"),
            (9704,"Contaminated Needle & Thread","Laceration"),
            (9705,"Sharp Needle & Thread","None"),
            (9706,"Oil-gunked Spanner","Vibration"),
            (9707,"Twinkling Scrap","None"),
            (9708,"Trash Crab Brain Wine","Burst"),
            (9709,"Pom-pom Hat","Breath"),
            (9710,"Huge Gift Sack","Breath"),
            (9711,"Sad Plushie","None"),
            (9712,"Black Ledger","None"),
            (9713,"Rusted Hilt","Slash"),
            (9714,"Fractured Blade","Laceration"),
            (9715,"Broken Blade","Breath"),
            (9716,"Red Tassel","Slash"),
            (9717,"Sublimity","Slash"),
            (9718,"Unbending","Slash"),
            (9719,"Ragged Bamboo Hat","Breath"),
            (9720,"Old Dopo Robe","Slash"),
            (9721,"Silver Watch Case","Vibration"),
            (9722,"Faded Watch Case","Vibration"),
            (9723,"Warning Notice","None"),
            (9724,"Etched Clock Hands","Vibration"),
            (9725,"Rusted Clock Hands","Vibration"),
            (9726,"Chalice of Trickle-down","Vibration"),
            (9727,"Prepaid Time Receipt","None"),
            (9728,"Pocket Watch : Type L","Vibration"),
            (9729,"Pocket Watch : Type E","Vibration"),
            (9730,"Pocket Watch : Type Y","Vibration"),
            (9731,"Pocket Watch : Type P","Vibration"),
            (9732,"Economy Class Discount Voucher","None"),
            (9733,"Canned Ice Cream","None"),
            (9734,"E-Type Dimensional Dagger","Charge"),
            (9735,"Portable Barrier Battery","Charge"),
            (9736,"Biogenerative Battery","Charge"),
            (9737,"Cardiovascular Reactive Module","Charge"),
            (9738,"Prosthetic Joint Servos","Charge"),
            (9739,"Crystallized Blood","Laceration"),
            (9740,"Automated Joints","Charge"),
            (9741,"Overcharged Battery","Charge"),
            (9742,"Perpetual Generator Servos","Charge"),
            (9743,"Hearts-powered Jewel","Charge"),
            (9744,"Filial Love","Laceration"),
            (9745,"Misaligned Transistor","Charge"),
            (9746,"Mental Corruption Boosting Gas","Sinking"),
            (9747,"Leaked Enkephalin","Sinking"),
            (9748,"Hardship","None"),
            (9749,"Crown of Thorns","None"),
            (9750,"Rest","Sinking"),
            (9751,"Snake Slough","None"),
            (9752,"False Halo","None"),
            (9753,"Metronome","None"),
            (9754,"Bridle","None"),
            (9755,"Contempt of the Gaze of Contempt","None"),
            (9756,"Unhatched Embers","Combustion"),
            (9757,"Anti-Ovine Grounding Plug","None"),
            (9758,"Vestiges of the King","None"),
            (9759,"Snuffed Lantern","Vibration"),
            (9760,"Snuffed Candlestick","None"),
            (9761,"Shadow Monster","Vibration"),
            (9762,"Packaging Box","None"),
            (9763,"(ID:9763)","None"),
            (9764,"(ID:9764)","None"),
            (9765,"(ID:9765)","Breath"),
            (9766,"(ID:9766)","None"),
            (9767,"(ID:9767)","Penetrate"),
            (9768,"(ID:9768)","Penetrate"),
            (9769,"(ID:9769)","None"),
            (9770,"(ID:9770)","Breath"),
            (9771,"(ID:9771)","Breath"),
            (9772,"(ID:9772)","Combustion"),
            (9773,"(ID:9773)","Burst"),
            (9774,"(ID:9774)","Combustion"),
            (9775,"(ID:9775)","Burst"),
            (9776,"(ID:9776)","Combustion"),
            (9777,"(ID:9777)","Burst"),
            (9778,"(ID:9778)","None"),
            (9779,"(ID:9779)","None"),
            (9780,"(ID:9780)","None"),
            (9781,"(ID:9781)","None"),
            (9782,"(ID:9782)","Slash"),
            (9783,"(ID:9783)","Laceration"),
            (9784,"(ID:9784)","Slash"),
            (9785,"(ID:9785)","Slash"),
            (9786,"(ID:9786)","Hit"),
            (9787,"(ID:9787)","Burst"),
            (9788,"(ID:9788)","Burst"),
            (9789,"(ID:9789)","Hit"),
            (9790,"(ID:9790)","Burst"),
            (9791,"(ID:9791)","Slash"),
            (9792,"(ID:9792)","Hit"),
            (9793,"(ID:9793)","Burst"),
            (9794,"(ID:9794)","None"),
            (9795,"(ID:9795)","Laceration"),
            (9796,"(ID:9796)","Breath"),
            (9797,"(ID:9797)","None"),
            (9798,"(ID:9798)","Laceration"),
            (9799,"(ID:9799)","None"),
            (9800,"(ID:9800)","None"),
            (9801,"(ID:9801)","None"),
            (9802,"(ID:9802)","Charge"),
            (9803,"(ID:9803)","Burst"),
            (9804,"(ID:9804)","Breath"),
            (9805,"(ID:9805)","None"),
            (9806,"(ID:9806)","Sinking"),
            (9807,"(ID:9807)","Sinking"),
            (9808,"(ID:9808)","Charge"),
            (9809,"(ID:9809)","Charge"),
            (9810,"(ID:9810)","None"),
            (9811,"(ID:9811)","None"),
            (9812,"(ID:9812)","Vibration"),
            (9813,"(ID:9813)","None"),
            (9814,"(ID:9814)","Vibration"),
            (9816,"(ID:9816)","Vibration"),
            (9817,"(ID:9817)","Sinking"),
            (9818,"(ID:9818)","Charge"),
            (9819,"(ID:9819)","Combustion"),
            (9820,"(ID:9820)","Vibration"),
            (9821,"(ID:9821)","Slash"),
            (9822,"(ID:9822)","Burst"),
            (9823,"(ID:9823)","Burst"),
            (9824,"(ID:9824)","Burst"),
            (9825,"(ID:9825)","Burst"),
            (9826,"(ID:9826)","None"),
            (9827,"(ID:9827)","Laceration"),
            (9828,"(ID:9828)","Vibration"),
            (9829,"(ID:9829)","Hit"),
            (9830,"(ID:9830)","None"),
            (9831,"(ID:9831)","None"),
            (9832,"(ID:9832)","None"),
            (9833,"(ID:9833)","None"),
            (9834,"(ID:9834)","None"),
            (9835,"(ID:9835)","None"),
            (9836,"(ID:9836)","Sinking"),
            (9837,"(ID:9837)","Burst"),
            (9838,"(ID:9838)","Breath"),
            (9839,"(ID:9839)","Vibration"),
            (9840,"(ID:9840)","Charge"),
            (9841,"(ID:9841)","Charge"),
            (9842,"(ID:9842)","Laceration"),
            (9843,"(ID:9843)","Laceration"),
            (9991,"(ID:9991)","None"),
            (9992,"(ID:9992)","None"),
            (9993,"(ID:9993)","None"),
            (9994,"(ID:9994)","None"),
            (9995,"(ID:9995)","None"),
            // ── 테마팩 전용 기프트 (2xxx) ──────────────────────────────────────
            (2027,"(ID:2027)","None"),
            (2028,"(ID:2028)","Slash"),
            (2029,"(ID:2029)","Laceration"),
            (2030,"(ID:2030)","Breath"),
            (2031,"(ID:2031)","Slash"),
            (2032,"(ID:2032)","Laceration"),
            (2033,"(ID:2033)","Breath"),
            (2034,"(ID:2034)","Breath"),
            (2035,"(ID:2035)","Slash"),
            (2036,"(ID:2036)","Vibration"),
            (2037,"(ID:2037)","Vibration"),
            (2038,"(ID:2038)","None"),
            (2039,"(ID:2039)","Vibration"),
            (2040,"(ID:2040)","Vibration"),
            (2041,"(ID:2041)","Vibration"),
            (2042,"(ID:2042)","None"),
            (2043,"(ID:2043)","Vibration"),
            (2044,"(ID:2044)","Vibration"),
            (2045,"(ID:2045)","Vibration"),
            (2046,"(ID:2046)","Vibration"),
            (2080,"(ID:2080)","Slash"),
            (2081,"(ID:2081)","Laceration"),
            (2082,"(ID:2082)","Slash"),
            // ── 스토리 던전 기프트 (1xxx) ──────────────────────────────────────
            (1052,"(ID:1052)","Combustion"),
            (1053,"(ID:1053)","Burst"),
            (1054,"(ID:1054)","None"),
            (1055,"(ID:1055)","Laceration"),
            (1056,"(ID:1056)","None"),
        };

        public InjectorUI(IntPtr ptr) : base(ptr) { }

        private void Start()
        {
            try
            {
                if (File.Exists(LetheGiftInjectorPlugin.TokenFilePath))
                {
                    _token = File.ReadAllText(LetheGiftInjectorPlugin.TokenFilePath).Trim();
                    _status = "저장된 토큰을 로드했다. Fetch State를 눌러라.";
                    LetheGiftInjectorPlugin.Log?.LogInfo("저장된 토큰 로드 완료");
                }
            }
            catch (Exception ex)
            {
                LetheGiftInjectorPlugin.Log?.LogWarning("토큰 로드 실패: " + ex.Message);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Backslash))
                _showPanel = !_showPanel;

            if (_pendingTask != null && _pendingTask.IsCompleted)
            {
                if (_pendingTask.IsFaulted)
                    _status = "오류: " + (_pendingTask.Exception?.InnerException?.Message ?? "알 수 없음");
                _pendingTask = null;
            }
        }

        private void OnGUI()
        {
            if (!_showPanel) return;

            GUI.Box(_windowRect, "Lethe Gift Injector ver.2.8.0");

            var titleBar = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, 20);
            var e = Event.current;
            if (e.type == EventType.MouseDown && titleBar.Contains(e.mousePosition))
            {
                _isDragging = true;
                _dragOffset = new Vector2(_windowRect.x - e.mousePosition.x,
                                          _windowRect.y - e.mousePosition.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDragging)
            {
                _windowRect.x = e.mousePosition.x + _dragOffset.x;
                _windowRect.y = e.mousePosition.y + _dragOffset.y;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _isDragging = false;
            }

            GUILayout.BeginArea(new Rect(
                _windowRect.x + 5,
                _windowRect.y + 22,
                _windowRect.width - 10,
                _windowRect.height - 27));

            GUILayout.Label("Token:");
            GUILayout.BeginHorizontal();
            _token = GUILayout.TextField(_token, GUILayout.Width(400));
            if (GUILayout.Button("저장", GUILayout.Width(50))) SaveToken();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            bool isBusy = _pendingTask != null;
            GUILayout.BeginHorizontal();
            GUI.enabled = !isBusy && !string.IsNullOrEmpty(_token);
            if (GUILayout.Button("① Fetch State", GUILayout.Width(150)))
                _pendingTask = FetchStateAsync();
            GUI.enabled = !isBusy && _fetched && _pendingGifts.Count > 0;
            if (GUILayout.Button("② Update State", GUILayout.Width(150)))
                _pendingTask = UpdateStateAsync();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(isBusy ? "[처리 중...]" : _status);
            GUILayout.Label($"대기 목록: {_pendingGifts.Count}개");

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label("ID:", GUILayout.Width(20));
            _inputId   = GUILayout.TextField(_inputId,   GUILayout.Width(80));
            GUILayout.Label("Tier:", GUILayout.Width(32));
            _inputTier = GUILayout.TextField(_inputTier, GUILayout.Width(28));
            if (GUILayout.Button("추가", GUILayout.Width(55))) TryAdd();
            if (GUILayout.Button("초기화", GUILayout.Width(55)))
            {
                _pendingGifts.Clear();
                _status = "대기 목록을 초기화했다.";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.Label("기프트 목록:");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _kwRow1.Length; i++)
            {
                int idx = i;
                if (GUILayout.Toggle(_kwIndex == idx, _kwRow1[i], "Button", GUILayout.Width(82)))
                { _kwIndex = idx; _giftPage = 0; }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _kwRow2.Length; i++)
            {
                int idx = i + _kwRow1.Length;
                if (GUILayout.Toggle(_kwIndex == idx, _kwRow2[i], "Button", GUILayout.Width(82)))
                { _kwIndex = idx; _giftPage = 0; }
            }
            GUILayout.EndHorizontal();

            string selKw = _allKeywords[_kwIndex];
            var filtered = new System.Collections.Generic.List<(int gid, string name, string kw)>();
            foreach (var g in _allGifts)
                if (selKw == "All" || g.kw == selKw) filtered.Add(g);

            if (GUILayout.Button($"현재 필터 전체 추가 ({filtered.Count}개)", GUILayout.Width(240)))
                AddAllFiltered(filtered);

            GUILayout.Space(2);

            int giftStart = _giftPage * PAGE_SIZE;
            int giftEnd   = Math.Min(giftStart + PAGE_SIZE, filtered.Count);
            for (int i = giftStart; i < giftEnd; i++)
            {
                var (gid, name, _) = filtered[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(name, GUILayout.Width(450));
                if (GUILayout.Button("+", GUILayout.Width(28))) AddToList(gid, 2);
                GUILayout.EndHorizontal();
            }

            int totalPages = Math.Max(1, (filtered.Count + PAGE_SIZE - 1) / PAGE_SIZE);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(35)) && _giftPage > 0)
                _giftPage--;
            GUILayout.Label($"{_giftPage + 1} / {totalPages}", GUILayout.Width(65));
            if (GUILayout.Button(">", GUILayout.Width(35)) && _giftPage < totalPages - 1)
                _giftPage++;
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void SaveToken()
        {
            try
            {
                File.WriteAllText(LetheGiftInjectorPlugin.TokenFilePath, _token.Trim());
                _status = "토큰 저장 완료";
            }
            catch (Exception ex)
            {
                _status = "토큰 저장 실패: " + ex.Message;
            }
        }

        private async Task FetchStateAsync()
        {
            _status = "Fetch 중...";
            _fetched = false;
            try
            {
                var reqBody  = $"{{\"token\":\"{EscapeJson(_token.Trim())}\"}}";
                var content  = new StringContent(reqBody, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(FETCH_URL, content);
                var respStr  = await response.Content.ReadAsStringAsync();

                LetheGiftInjectorPlugin.Log?.LogInfo(
                    $"Fetch 응답 전체 ({(int)response.StatusCode}): {respStr}");

                if (!response.IsSuccessStatusCode)
                {
                    _status = $"Fetch 실패 ({(int)response.StatusCode}) — 로그 확인";
                    return;
                }

                _state = JsonNode.Parse(respStr);

                var currentInfo = _state?["currentInfo"];
                if (currentInfo == null)
                {
                    _status = "[오류] 응답에 'currentInfo' 없음 — 로그에서 키 이름 확인";
                    LetheGiftInjectorPlugin.Log?.LogError(
                        "FetchStateAsync: currentInfo is null.");
                    return;
                }

                var egsNode = currentInfo["egs"];
                if (egsNode == null)
                {
                    LetheGiftInjectorPlugin.Log?.LogWarning(
                        "FetchStateAsync: 'egs' 키 없음. currentInfo: "
                        + currentInfo.ToJsonString());
                    _status = "[경고] egs 키 없음 — 로그 확인";
                    _fetched = true;
                    return;
                }

                _egsKey  = "egs";
                _fetched = true;
                _status  = $"Fetch 성공. 현재 기프트: {egsNode.AsArray().Count}개";
            }
            catch (Exception ex)
            {
                _status = "Fetch 오류: " + ex.Message;
                LetheGiftInjectorPlugin.Log?.LogError("Fetch 오류: " + ex);
            }
        }

        private async Task UpdateStateAsync()
        {
            if (_state == null) { _status = "[오류] state 없음. Fetch 먼저 실행"; return; }
            _status = "Update 중...";
            try
            {
                var currentInfo = _state["currentInfo"];
                if (currentInfo == null)
                {
                    _status = "[오류] currentInfo 없음 — 로그 확인";
                    LetheGiftInjectorPlugin.Log?.LogError(
                        "UpdateStateAsync: currentInfo is null.");
                    return;
                }

                var egsNode = currentInfo[_egsKey];
                if (egsNode == null)
                {
                    _status = $"[오류] '{_egsKey}' 키 없음 — 로그 확인";
                    LetheGiftInjectorPlugin.Log?.LogError(
                        "UpdateStateAsync: egs node is null.");
                    return;
                }

                var egs   = egsNode.AsArray();
                int added = 0;
                foreach (var (gid, gul) in _pendingGifts)
                {
                    bool exists = false;
                    foreach (var eg in egs)
                        if (eg?["id"]?.GetValue<int>() == gid) { exists = true; break; }
                    if (exists) continue;

                    egs.Add(new JsonObject
                    {
                        ["id"]   = gid,
                        ["pids"] = new JsonArray(),
                        ["un"]   = 0,
                        ["ul"]   = gul
                    });
                    added++;
                }

                var payload = new JsonObject
                {
                    ["token"]    = _token.Trim(),
                    ["saveInfo"] = JsonNode.Parse(_state.ToJsonString())
                };

                var content  = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(UPDATE_URL, content);
                var respStr  = await response.Content.ReadAsStringAsync();

                LetheGiftInjectorPlugin.Log?.LogInfo(
                    $"Update 응답 ({(int)response.StatusCode}): {respStr}");

                if (!response.IsSuccessStatusCode)
                {
                    _status = $"Update 실패 ({(int)response.StatusCode}) — 로그 확인";
                    return;
                }

                _status = $"Update 성공. {added}개 추가됨. 던전 입장 시 적용.";
                _pendingGifts.Clear();
            }
            catch (Exception ex)
            {
                _status = "Update 오류: " + ex.Message;
                LetheGiftInjectorPlugin.Log?.LogError("Update 오류: " + ex);
            }
        }

        private void TryAdd()
        {
            if (!int.TryParse(_inputId, out int id))
            { _status = "[오류] 유효한 숫자 아님"; return; }
            if (!int.TryParse(_inputTier, out int tier) || tier < 0 || tier > 2) tier = 2;
            AddToList(id, tier);
        }

        private void AddToList(int id, int ul)
        {
            foreach (var (eid, _) in _pendingGifts)
                if (eid == id) { _status = $"ID {id} 이미 목록에 있음"; return; }
            _pendingGifts.Add((id, ul));
            _status = $"ID {id} (Tier {ul}) 추가됨";
        }

        private void AddAllFiltered(
            System.Collections.Generic.List<(int gid, string name, string kw)> filtered)
        {
            int added = 0;
            foreach (var (gid, _, _) in filtered)
            {
                bool exists = false;
                foreach (var (eid, _) in _pendingGifts)
                    if (eid == gid) { exists = true; break; }
                if (!exists) { _pendingGifts.Add((gid, 2)); added++; }
            }
            _status = $"필터 기준 {added}개 추가됨 (중복 제외)";
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}