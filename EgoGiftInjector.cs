// LetheGiftInjector (ver.2.9.2)
// - English translation patch
// - Added gift name search field (stacks with keyword filter)
// - FIXED: Reverted to base IDs and implemented 'maxTier' variable for upgradeable gifts.

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
    [BepInPlugin("com.mod.lethegiftinjector", "LetheGiftInjector", "2.9.2")]
    public class LetheGiftInjectorPlugin : BasePlugin
    {
        internal static new ManualLogSource? Log;
        internal static string TokenFilePath =>
            Path.Combine(Paths.ConfigPath, "LetheGiftInjector_token.txt");

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo("LetheGiftInjector v2.9.2 loaded");
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
        private string _inputTier = "0"; // Manual input default
        private string _searchQuery = "";

        private int _giftPage = 0;
        private const int PAGE_SIZE = 8;

        private Rect    _windowRect  = new Rect(20, 20, 540, 680);
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

        // Tuple format: (ID, Name, Keyword, MaxTier)
        private readonly (int gid, string name, string kw, int maxTier)[] _allGifts = {
            // ── Story Dungeon Gifts (1xxx) ────────────────────────────────────
            (1001,"Mask of a Devotee","None", 0),
            (1002,"Shoddy Dressing","None", 0),
            (1003,"Festered Fragment","None", 0),
            (1004,"Bloodied Mask of a Devotee","None", 0),
            (1005,"Ebony Brooch","None", 0),
            (1006,"Etched Doomsday","Combustion", 0),
            (1011,"Toy Fist","None", 0),
            (1012,"Toy Foot","None", 0),
            (1013,"Toy Screw","None", 0),
            (1014,"Rainbow Mainspring","None", 0),
            (1015,"Rusty Mainspring","None", 0),
            (1016,"Emergency Surgical Kit","None", 0),
            (1017,"Hopeful Eyes","None", 0),
            (1018,"Writhing Ribbon","None", 0),
            (1021,"A Sign","None", 0),
            (1022,"A Sign","None", 0),
            (1023,"A Sign","None", 0),
            (1024,"A Sign","None", 0),
            (1031,"Piece of Courage","None", 0),
            (1032,"Torch Stack","None", 0),
            (1033,"Old Warehouse’s Key","None", 0),
            (1034,"Blood-red Mane","None", 0),
            (1037,"N Corp. Seal","None", 0),
            (1038,"Token of Innocence","None", 0),
            (1039,"Token of Tears","None", 0),
            (1040,"Sparkling Skull","None", 0),
            (1041,"Hemorrhagic Hand","None", 0),
            (1042,"Sniggering Tongue","None", 0),
            (1043,"Token of Atonement","None", 0),
            (1044,"Ritualist’s Right","None", 0),
            (1046,"Green Skin","None", 0),
            (1048,"Topfhelm","None", 0),
            (1049,"Hammer","Vibration", 0),
            (1050,"Nagel","Laceration", 0),
            (1051,"Nagel und Hammer Scriptures","None", 0),
            (1052,"Hot ‘n Juicy Drumstick","Combustion", 0),
            (1053,"Dry-to-the-Bone Breast","Burst", 0),
            (1054,"Tango Marinade","None", 0),
            (1055,"Contaminated Needle & Thread","Laceration", 0),
            (1056,"Sharp Needle & Thread","None", 0),

            // ── Theme Pack Gifts (2xxx) ───────────────────────────────────────
            (2001,"Small Ampule","None", 0),
            (2002,"Piece of Crumbled Egg","None", 0),
            (2003,"Cold Illusion","None", 0),
            (2004,"Sharp Illusion","None", 0),
            (2005,"Fragrant Illusion","None", 0),
            (2006,"Trace of Stars","None", 0),
            (2007,"Pungent Bud","Sinking", 0),
            (2008,"Hot Tears","None", 0),
            (2009,"Cooled Tear","None", 0),
            (2010,"Memories of Fragments","None", 0),
            (2011,"Piece of a Torn Spring","None", 0),
            (2012,"Piece of a Torn Summer","None", 0),
            (2013,"Piece of a Torn Autumn","None", 0),
            (2014,"Piece of a Torn Winter","None", 0),
            (2015,"Piece of a Broken Bond","None", 0),
            (2016,"Piece of Reversing Memories","None", 0),
            (2017,"Noose","None", 0),
            (2018,"Reverse Knot","None", 0),
            (2019,"Melted LCCB Employee Badge","None", 0),
            (2020,"Desperate's Knot","None", 0),
            (2021,"Mermaid Oil","Vibration", 0),
            (2022,"Crossed Knot","None", 0),
            (2023,"Compulsive's Knot","None", 0),
            (2024,"Rusted Hook","Breath", 0),
            (2025,"Standard Issue Mag","None", 0),
            (2026,"Unbreakable Knot","None", 0),
            (2027,"Black Ledger","None", 0),
            (2028,"Rusted Hilt","Slash", 0),
            (2029,"Fractured Blade","Laceration", 0),
            (2030,"Broken Blade","Breath", 0),
            (2031,"Red Tassel","Slash", 0),
            (2032,"Sublimity","Slash", 0),
            (2033,"Unbending","Slash", 0),
            (2034,"Ragged Bamboo Hat","Slash", 0),
            (2035,"Old Dopo Robe","Slash", 0),
            (2036,"Silver Watch Case","Vibration", 0),
            (2037,"Faded Watch Case","Vibration", 0),
            (2038,"Warning Notice","None", 0),
            (2039,"Etched Clock Hands","Vibration", 0),
            (2040,"Rusted Clock Hands","Vibration", 0),
            (2041,"Chalice of Trickle-down","Vibration", 0),
            (2042,"Prepaid Time Receipt","None", 0),
            (2043,"Pocket Watch : Type L","Vibration", 0),
            (2044,"Pocket Watch : Type E","Vibration", 0),
            (2045,"Pocket Watch : Type Y","Vibration", 0),
            (2046,"Pocket Watch : Type P","Vibration", 0),
            (2048,"La Manchaland All-day Pass","Breath", 0),
            (2049,"La Manchaland Standard Pass","Laceration", 0),
            (2050,"Token of Victory","None", 0),
            (2051,"Self-flagellation Whip","Laceration", 0),
            (2052,"Mask of the Parade","Laceration", 0),
            (2053,"Devouring Cube","None", 0),
            (2054,"Endless Hunger","Laceration", 0),
            (2055,"A Little Dream","Laceration", 0),
            (2056,"A Hollow Dream","Laceration", 0),
            (2057,"The Dream Begins Anew","None", 0),
            (2058,"Grimy Iron Stake","Laceration", 0),
            (2059,"Barbed Lasso","Burst", 0),
            (2060,"Carmilla","None", 0),
            (2061,"Coin","None", 0),
            (2062,"Worn Entry Card","None", 0),
            (2063,"Heavenly King's Heartmending Pill","None", 0),
            (2064,"Tranquil Lotus Bolus","None", 0),
            (2065,"Sanguine Blossom Bolus","None", 0),
            (2066,"Tenacity Bolus","None", 0),
            (2067,"Flower in the Mirror","Burst", 0),
            (2068,"Accursed Codex","None", 0),
            (2069,"Madness-inscribed Codex","None", 0),
            (2070,"Moon in the Water","Breath", 0),
            (2071,"Flower Mound","Burst", 0),
            (2072,"Lightning Axe","Charge", 0),
            (2073,"Heartrender Talisman","None", 0),
            (2074,"Bloodsevering Blade","Laceration", 0),
            (2075,"Jin Gang Bolus","None", 0),
            (2076,"Faded Poster","Breath", 0),
            (2077,"Deep Claw Marks","None", 0),
            (2080,"Worn Hilt","Slash", 0),
            (2081,"Resplendence","Laceration", 0),
            (2082,"Cultivation","Slash", 0),

            // ── Normal & Upgraded Gifts ───────────────────────────────────────
            (9001,"Hellterfly's Dream","Combustion", 2),
            (9002,"Perversion","None", 0),
            (9003,"Ashes to Ashes","Combustion", 2),
            (9004,"Phlebotomy Pack","None", 0),
            (9005,"Wound Clerid","Laceration", 2),
            (9006,"Coffee and Cranes","None", 0),
            (9007,"Eclipse of Scarlet Moths","None", 0),
            (9008,"Grimy Iron Stake","Laceration", 0),
            (9009,"Fiery Down","Combustion", 2),
            (9010,"Bloody Gadget","None", 2),
            (9011,"Sunshower","None", 0),
            (9012,"Today's Expression","Hit", 0),
            (9013,"Talisman Bundle","Burst", 2),
            (9014,"Rusty Commemorative Coin","None", 0),
            (9015,"Blood, Sweat, and Tears","None", 2),
            (9016,"Green Spirit","Vibration", 2),
            (9017,"Lithograph","None", 0),
            (9018,"Crown of Roses","Burst", 2),
            (9019,"Sticky Muck","Penetrate", 0),
            (9020,"White Gossypium","Laceration", 2),
            (9021,"Blue Zippo Lighter","None", 0),
            (9022,"Phantom Pain","None", 0),
            (9023,"Thunderbranch","Burst", 2),
            (9024,"Melted Eyeball","Vibration", 0),
            (9025,"Grey Coat","None", 0),
            (9026,"Late-bloomer's Tattoo","None", 0),
            (9027,"Lowest Star","Hit", 2),
            (9028,"Prejudice","None", 0),
            (9029,"Little and To-be-Naughty Plushie","Laceration", 0),
            (9030,"Gathering Skulls","Penetrate", 0),
            (9031,"Nixie Divergence","Vibration", 2),
            (9032,"Dreaming Electric Sheep","Slash", 0),
            (9033,"Standard-duty Battery","Burst", 0),
            (9034,"Pinpoint Logic Circuit","Combustion", 0),
            (9035,"Voodoo Doll","None", 2),
            (9036,"Carmilla","None", 2),
            (9037,"Child within a Flask","None", 0),
            (9038,"Illusory Hunt","None", 0),
            (9039,"Homeward","None", 0),
            (9040,"Tomorrow's Fortune","None", 0),
            (9041,"Red Order","Sinking", 2),
            (9042,"Smokes and Wires","Laceration", 2),
            (9043,"Employee Card","Charge", 2),
            (9044,"Oscillating Bracelet","Vibration", 2),
            (9045,"Glimpse of Flames","Combustion", 2),
            (9046,"Cigarette Holder","Breath", 0),
            (9047,"Barbed Lasso","Burst", 2),
            (9048,"Rusted Cutting Knife","Laceration", 2),
            (9049,"Thorny Path","Sinking", 2),
            (9050,"Red-stained Gossypium","Laceration", 2),
            (9051,"Stone Tomb","Breath", 2),
            (9052,"Portable Battery Socket","Charge", 2),
            (9053,"Dust to Dust","Combustion", 2),
            (9054,"Melted Spring","Sinking", 0),
            (9055,"Downpour","Vibration", 2),
            (9056,"Four-leaf Clover","Breath", 0),
            (9057,"Nightvision Goggles","Charge", 0),
            (9058,"Disk Fragment","None", 0),
            (9059,"Midwinter Nightmare","Sinking", 0),
            (9060,"Thrill","Burst", 2),
            (9061,"Skeletal Crumbs","Sinking", 0),
            (9062,"Curriculum Vitae","Charge", 2),
            (9063,"Pendant of Nostalgia","Breath", 2),
            (9064,"Broken Revolver","Burst", 0),
            (9065,"Artistic Sense","Sinking", 2),
            (9066,"Nebulizer","Breath", 2),
            (9067,"Special Contract","None", 0),
            (9068,"Grand Welcome","None", 0),
            (9069,"Wrist Guards","Charge", 2),
            (9070,"Clear Mirror, Calm Water","Breath", 2),
            (9071,"Charred Disk","Combustion", 0),
            (9072,"Lightning Rod","Charge", 2),
            (9073,"Endorphin Kit","Breath", 2),
            (9074,"Headless Portrait","Sinking", 2),
            (9075,"Charge-type Gloves","Charge", 2),
            (9076,"First-aid Kit","None", 0),
            (9077,"Painkiller","None", 0),
            (9078,"Voracious Hammer","None", 0),
            (9079,"Golden Urn","None", 0),
            (9080,"Milepost of Survival","None", 0),
            (9081,"Faith","None", 0),
            (9082,"Piece of Relationship","None", 2),
            (9083,"Lunar Memory","None", 0),
            (9084,"Ancient Effigy","None", 0),
            (9085,"Non-disclosure Agreement","None", 0),
            (9086,"Reverberation","Vibration", 2),
            (9087,"Burning Intellect","Combustion", 2),
            (9088,"Soothe the Dead","Combustion", 0),
            (9089,"Rusted Muzzle","Laceration", 2),
            (9090,"Bloody Mist","Laceration", 0),
            (9091,"Bell of Truth","Vibration", 2),
            (9092,"Coupled Oscillation","Vibration", 0),
            (9093,"Fluorescent Lamp","Burst", 2),
            (9094,"Enrapturing Trance","Burst", 0),
            (9095,"Broken Compass","Sinking", 2),
            (9096,"Black Sheet Music","Sinking", 0),
            (9097,"Ornamental Horseshoe","Breath", 2),
            (9098,"Lucky Pouch","Breath", 0),
            (9099,"Material Interference Force Field","Charge", 2),
            (9100,"T-1 Perpetual Motion Machine","Charge", 0),
            (9101,"Melted Paraffin","Combustion", 2),
            (9102,"Polarization","Combustion", 2),
            (9103,"Pain of Stifled Rage","Combustion", 2),
            (9104,"Ardent Flower","Combustion", 0),
            (9105,"Fragment of Hellfire","Combustion", 0),
            (9106,"Arrested Hymn","Laceration", 2),
            (9107,"Tangled Bundle","Laceration", 2),
            (9108,"Awe","Laceration", 2),
            (9109,"Respite","Laceration", 0),
            (9110,"Fragment of Allurement","Laceration", 0),
            (9111,"Bio-venom Vial","Vibration", 2),
            (9112,"Venomous Skin","Vibration", 2),
            (9113,"Sour Liquor Aroma","Vibration", 2),
            (9114,"Mirror Tactile Synaesthesia","Vibration", 0),
            (9115,"Clockwork Spring","Vibration", 0),
            (9116,"Fragment of Inertia","Vibration", 0),
            (9117,"Smoking Gunpowder","Burst", 2),
            (9118,"Bone Stake","Burst", 0),
            (9119,"Ragged Umbrella","Burst", 2),
            (9120,"Deathseeker","Burst", 0),
            (9121,"Fragment of Desire","Burst", 0),
            (9122,"Eldtree Snare","Sinking", 2),
            (9123,"Rags","Sinking", 2),
            (9124,"Grandeur","Sinking", 2),
            (9125,"Distant Star","Sinking", 0),
            (9126,"Fragment of Decay","Sinking", 0),
            (9127,"Devil's Share","Breath", 2),
            (9128,"Emerald Elytra","Breath", 2),
            (9129,"Old Wooden Doll","Breath", 2),
            (9130,"Finifugality","Breath", 0),
            (9131,"Fragment of Conceit","Breath", 0),
            (9132,"UPS System","Charge", 2),
            (9133,"Uncapped Defibrilator","Charge", 2),
            (9134,"Patrolling Flashlight","Charge", 2),
            (9135,"Imitative Generator","Charge", 0),
            (9136,"Fragment of Friction","Charge", 0),
            (9137,"Scalpel","Slash", 2),
            (9138,"Deceptive Accord","Slash", 2),
            (9139,"Tailor's Scissors","Slash", 2),
            (9140,"Resolution","Slash", 2),
            (9141,"Moment of Sentencing","Slash", 2),
            (9142,"Sundered Memory","Slash", 0),
            (9143,"Carpenter's Nail","Penetrate", 2),
            (9144,"Once, A Blessing","Penetrate", 2),
            (9145,"Torn Bandolier","Penetrate", 2),
            (9146,"Keenbranch","Penetrate", 2),
            (9147,"Punctured Memory","Penetrate", 0),
            (9148,"Burial Curse","Hit", 2),
            (9149,"Compression Bandage","Hit", 2),
            (9150,"Temporal Bridle","Hit", 2),
            (9151,"Clasped Sculpture","Hit", 2),
            (9152,"Crushed Memory","Hit", 0),
            (9153,"Oracle","None", 0),
            (9154,"Imposed Weight","None", 0),

            // ── Seasonal & Special Gifts ──────────────────────────────────────
            (9155,"Decamillennial Stewpot","Combustion", 0),
            (9156,"Decamillennial Hearthflame","Combustion", 0),
            (9157,"Secret Cookbook","Combustion", 0),
            (9158,"Purloined Flame","Combustion", 0),
            (9159,"Millarca","Laceration", 0),
            (9160,"Ruptured Blood Sac","Laceration", 0),
            (9161,"Devotion","Laceration", 0),
            (9162,"Hemorrhagic Shock","Laceration", 0),
            (9163,"Gemstone Oscillator","Vibration", 0),
            (9164,"Wobbling Keg","Vibration", 0),
            (9165,"Interlocked Cogs","Vibration", 0),
            (9166,"Epicenter","Vibration", 0),
            (9167,"Omnivibro-octovecti-bell","Vibration", 0),
            (9168,"Shard of Apocalypse","Burst", 0),
            (9169,"Thorny Rope Cuffs","Burst", 0),
            (9170,"Eerie Effigy","Burst", 0),
            (9171,"Ruin","Burst", 0),
            (9172,"Cantabile","Sinking", 0),
            (9173,"Faded Overcoat","Sinking", 0),
            (9174,"Tangled Bones","Sinking", 0),
            (9175,"Surging Globe","Sinking", 0),
            (9176,"Impending Wave","Sinking", 0),
            (9177,"Recollection of a Certain Day","Breath", 0),
            (9178,"Angel's Cut","Breath", 0),
            (9179,"Reminiscence","Breath", 0),
            (9180,"Cask Spirits","Breath", 0),
            (9181,"Miniature Telepole","Charge", 0),
            (9182,"T-1B Octagonal Bolt","Charge", 0),
            (9183,"Insulator","Charge", 0),
            (9184,"T-5 Perpetual Motion Machine","Charge", 0),
            (9185,"Rebate Token","None", 0),
            (9186,"New Release Pamphlet","None", 0),
            (9187,"Special Catalogue","None", 0),
            (9188,"Pre-order Discount","None", 0),
            (9189,"Renewed Merch","None", 0),
            (9190,"Trial Plan Guide","None", 0),
            (9191,"Prestige Card","None", 0),
            (9192,"Layered Bandages","None", 1),
            (9193,"Overused Whetstone","Slash", 0),
            (9194,"Short Cane Sword","None", 2),
            (9195,"Cloudpattern Gourd Bottle","Slash", 2),
            (9196,"Broken Greatsword","Slash", 0),
            (9197,"High-tensility Shoes","None", 0),
            (9198,"Plume of Proof","None", 2),
            (9199,"Torn Hems","None", 2),
            (9200,"Dueling Manual Book 3","None", 0),
            (9201,"Dimensional Recycle Bin","Hit", 0),
            (9202,"Pocket Flashcards","None", 2),
            (9203,"Dimensional Perception Modifier","Hit", 0),
            (9204,"The Book of Vengeance","Hit", 0),
            (9205,"WB Flask","None", 0),
            (9206,"Sanguine Fragrance Descends","Laceration", 0),
            (9207,"Chance & Choice","None", 0),
            (9208,"Entangled Fate","None", 0),
            (9209,"For You Who Love the City","None", 0),
            (9210,"Meat Tenderizer","Hit", 2),
            (9211,"Raincloud","Sinking", 0),
            (9212,"The End of all Evil","Breath", 0),
            (9213,"Miniature Ferris Wheel","Laceration", 0),
            (9214,"Searing Hammer","Combustion", 0),
            (9215,"Red Necktie","Combustion", 0),
            (9216,"Uniform - Liu Assoc.","Combustion", 0),
            (9217,"Someone's Device","Sinking", 0),
            (9218,"Providence of the Prescript","Breath", 0),
            (9219,"Omni-tool","None", 2),
            (9220,"Stolen Fixer Magazine","Hit", 0),
            (9221,"Thermal Weapon","Laceration", 0),
            (9222,"Pure-white Canvas","Laceration", 0),
            (9223,"Average Artwork","Laceration", 0),
            (9224,"Exquisite Artwork","Laceration", 0),
            (9225,"The Spider's Thread","None", 2),
            (9226,"Someone's Azure Blade","Breath", 0),
            (9227,"Baleful Hwando","None", 0),
            (9228,"One with the Blade","None", 0),
            (9229,"Tarnished Gauntlet","None", 0),
            (9230,"Golden Hour","None", 0),
            (9231,"Record Logs of That Day","None", 0),
            (9232,"Potentialities","None", 0),
            (9233,"Staticky Two-way Radio","Sinking", 0),
            (9234,"Beak-shaped Necklace","Laceration", 0),
            (9235,"Desperado","Penetrate", 0),
            (9236,"Harmonics","Laceration", 2),
            (9237,"Walking Bass","Laceration", 2),
            (9238,"Pink Petals","Sinking", 0),
            (9239,"Pink Bouquet","Sinking", 0),
            (9240,"Royal Jelly Perfume","Combustion", 0),
            (9241,"Still-warm Coffee","None", 0),
            (9242,"Bongy Plush","None", 0),
            (9243,"Trial Extraction : A.B.S","Combustion", 0),
            (9244,"Trial Extraction : Lantern","Burst", 0),
            (9245,"Modified Extraction : AEDD","Charge", 0),
            (9246,"Someone's Dropped Employee Card","None", 0),
            (9247,"(ID:9247)","None", 0),
            (9248,"Spicebush, Glasses, and Mailed Letter","Burst", 0),
            (9249,"A Small, Lovely Violin","Hit", 0),
            (9256,"Imperfect Eye of Precognition","Vibration", 0),
            (9257,"The Abandoned Oracle","Sinking", 0),
            (9258,"The Book of Vengeance: Annex","Hit", 0),
            (9259,"A Maestro's Ring-Turned-Artwork","Laceration", 0),
            (9260,"Artwork: Pulsation","Charge", 0),
            (9261,"Artwork: Ferity","Sinking", 0),
            (9262,"Universal Skeleton","None", 0),
            (9263,"Artwork: Evolving Pulsation","Laceration", 0),
            (9264,"Artwork: Taxidermied Ferity","Laceration", 0),
            (9265,"The Middle Styled Knuckle-dusters","Hit", 0),
            (9266,"Unoccupied Birdcage","None", 0),
            (9267,"Searing Brass","Combustion", 2),
            (9268,"Universal Instinct","Laceration", 0),

            (9403,"Ebony Brooch","Burst", 0),
            (9404,"Contained Maggots","Laceration", 0),
            (9407,"Made-to-Order","None", 0),
            (9408,"Haunted Shoes","None", 0),
            (9409,"Frozen Cries","Sinking", 0),
            (9410,"Hoarfrost Footprint","Sinking", 0),
            (9413,"Nagel und Hammer Scriptures","Laceration", 0),
            (9414,"Blood-red Mane","None", 0),
            (9415,"Squalidity","Laceration", 0),
            (9416,"Wholeness","Laceration", 0),
            (9419,"Spicebush Branch","None", 0),
            (9420,"Kaleidoscope","None", 0),
            (9423,"Broken Glasses","None", 0),
            (9424,"Unmailed Letter","Burst", 0),
            (9427,"Town-protecting Harpoon","None", 2),
            (9428,"Cetacean Heart","Breath", 0),
            (9429,"Harpoon Prosthetic Leg","Breath", 0),
            (9430,"Guiding Gas Lamp","Breath", 0),
            (9431,"Broken Violin","None", 2),
            (9432,"Manor-shaped Music Box","Sinking", 0),
            (9433,"Chief Butler's Secret Arts","None", 0),
            (9434,"Handheld Mirror","None", 0),
            (9435,"Butler Style Binding Arts","None", 2),
            (9436,"Refraction Glass Pod","Sinking", 0),
            (9437,"La Manchaland All-day Pass","Breath", 0),
            (9438,"Token of Victory","Laceration", 0),
            (9439,"Devouring Cube","None", 0),
            (9440,"Mask of the Parade","Laceration", 1),

            (9701,"Hot ‘n Juicy Drumstick","Combustion", 0),
            (9702,"Dry-to-the-Bone Breast","Burst", 0),
            (9703,"Tango Marinade","None", 0),
            (9704,"Contaminated Needle & Thread","Laceration", 0),
            (9705,"Sharp Needle & Thread","None", 0),
            (9706,"Oil-gunked Spanner","Vibration", 0),
            (9707,"Twinkling Scrap","None", 0),
            (9708,"Trash Crab Brain Wine","Burst", 0),
            (9709,"Pom-pom Hat","Breath", 0),
            (9710,"Huge Gift Sack","Breath", 0),
            (9711,"Sad Plushie","None", 0),
            (9712,"Black Ledger","None", 0),
            (9713,"Rusted Hilt","Slash", 0),
            (9714,"Fractured Blade","Laceration", 0),
            (9715,"Broken Blade","Breath", 0),
            (9716,"Red Tassel","Slash", 0),
            (9717,"Sublimity","Slash", 0),
            (9718,"Unbending","Slash", 0),
            (9719,"Ragged Bamboo Hat","Slash", 0),
            (9720,"Old Dopo Robe","Slash", 0),
            (9721,"Silver Watch Case","Vibration", 0),
            (9722,"Faded Watch Case","Vibration", 0),
            (9723,"Warning Notice","None", 0),
            (9724,"Etched Clock Hands","Vibration", 0),
            (9725,"Rusted Clock Hands","Vibration", 0),
            (9726,"Chalice of Trickle-down","Vibration", 0),
            (9727,"Prepaid Time Receipt","None", 0),
            (9728,"Pocket Watch : Type L","Vibration", 0),
            (9729,"Pocket Watch : Type E","Vibration", 0),
            (9730,"Pocket Watch : Type Y","Vibration", 0),
            (9731,"Pocket Watch : Type P","Vibration", 0),
            (9732,"Economy Class Discount Voucher","None", 0),
            (9733,"Canned Ice Cream","None", 0),
            (9734,"E-Type Dimensional Dagger","Charge", 0),
            (9735,"Portable Barrier Battery","Charge", 0),
            (9736,"Biogenerative Battery","Charge", 0),
            (9737,"Cardiovascular Reactive Module","Charge", 0),
            (9738,"Prosthetic Joint Servos","Charge", 0),
            (9739,"Crystallized Blood","Laceration", 0),
            (9740,"Automated Joints","Charge", 0),
            (9741,"Overcharged Battery","Charge", 0),
            (9742,"Perpetual Generator Servos","Charge", 0),
            (9743,"Hearts-powered Jewel","Charge", 0),
            (9744,"Filial Love","Laceration", 0),
            (9745,"Misaligned Transistor","Charge", 0),
            (9746,"Mental Corruption Boosting Gas","Sinking", 0),
            (9747,"Leaked Enkephalin","Sinking", 0),
            (9748,"Hardship","None", 0),
            (9749,"Crown of Thorns","None", 0),
            (9750,"Rest","Sinking", 0),
            (9751,"Snake Slough","None", 2),
            (9752,"False Halo","None", 2),
            (9753,"Metronome","None", 2),
            (9754,"Bridle","None", 2),
            (9755,"Contempt of the Gaze of Contempt","None", 2),
            (9756,"Unhatched Embers","Combustion", 2),
            (9757,"Anti-Ovine Grounding Plug","None", 1),
            (9758,"Vestiges of the King","None", 0),
            (9759,"Snuffed Lantern","Vibration", 0),
            (9760,"Snuffed Candlestick","None", 0),
            (9761,"Shadow Monster","Vibration", 0),
            (9762,"Packaging Box","None", 0),
            (9763,"Packaging Ribbon","None", 0),
            (9764,"Gift","None", 0),
            (9765,"Jolly Plushie","Breath", 0),
            (9766,"Implicit Contract Renewal","None", 0),
            (9767,"Darkflame Smoking Pipe","Penetrate", 0),
            (9768,"Equalizer","Penetrate", 0),
            (9769,"Swift Command","None", 0),
            (9770,"Gear Shrapnel","Breath", 0),
            (9771,"CQC Manual","Breath", 0),
            (9772,"Combustion Gloves","Combustion", 0),
            (9773,"Spiked Combat Boots","Burst", 0),
            (9774,"Re-ignition Plug","Combustion", 0),
            (9775,"Enhancer Mk.4","Burst", 0),
            (9776,"Embers","Combustion", 0),
            (9777,"Twigs","Burst", 0),
            (9778,"Regular Operational Gear","None", 0),
            (9779,"Operation Authorization Card","None", 0),
            (9780,"High-risk Operational Gear","None", 0),
            (9781,"Hardwood Liquor Cup","None", 0),
            (9782,"Worn Hilt","Slash", 0),
            (9783,"Resplendence","Laceration", 0),
            (9784,"Cultivation","Slash", 0),
            (9785,"Swarmcloud","Slash", 0),
            (9786,"Enh. Tattoos - The Middle","Hit", 0),
            (9787,"Strange Glyph Talisman","Burst", 0),
            (9788,"Harestride","Burst", 0),
            (9789,"Chains of Loyalty","Hit", 0),
            (9790,"Strange Glyph Inscriptions","Burst", 0),
            (9791,"Shadow Bamboo Hat","Slash", 0),
            (9792,"Everlasting Chains of Bond","Hit", 0),
            (9793,"Glyph of Glass Shards","Burst", 0),
            (9794,"Swishing Fuel Tank","None", 0),
            (9795,"A Drop","Laceration", 0),
            (9796,"Sorrowful Exhale","Breath", 0),
            (9797,"Shatterbound Cannon","None", 0),
            (9798,"Coveting Thorn","Laceration", 0),
            (9800,"Wealth","None", 0),
            (9801,"Tenacity Bolus","None", 0),
            (9802,"Lightning Axe","Charge", 0),
            (9803,"Flower in the Mirror","Burst", 0),
            (9804,"Moon in the Water","Breath", 0),
            (9805,"Ashen Constellation's Blessing","None", 0),
            (9806,"The Unchosen","Sinking", 0),
            (9807,"Sword Sharpened with Tears","Sinking", 0),
            (9808,"Magical Girl's Lovely Gift","Charge", 0),
            (9809,"Spent Use, Forming Hate","Charge", 0),
            (9810,"Trauma Shield","None", 0),
            (9811,"Value Disposal","None", 0),
            (9812,"Interlinked Time","Vibration", 2),
            (9813,"Emergency Investigator Badge","None", 2),
            (9814,"Entanglement Override Sequencer","Vibration", 2),
            (9816,"Microprecision Time Accelerator","Vibration", 0),
            (9817,"Blue Starshard","Sinking", 2),
            (9818,"Discarded Dimensional Gauntlet","Charge", 2),
            (9819,"Bloodflame Sword","Combustion", 0),
            (9820,"Blackiron Barding","Vibration", 0),
            (9821,"Red Cloth that Closes the Heart","Slash", 0),
            (9822,"Virtue - Zhi (智)","Burst", 0),
            (9823,"Virtue - Yong (勇)","Burst", 0),
            (9824,"Virtue - Ren (仁)","Burst", 0),
            (9825,"Cultivation: Cut, File, Carve, Polish","Burst", 0),
            (9826,"Shaoxing Wine","None", 2),
            (9827,"The Family's Resentment","Laceration", 0),
            (9828,"For the Capo","Vibration", 0),
            (9829,"Rules of the Middle","Hit", 0),
            (9830,"Kkomi's Mini-Gift","None", 0),
            (9831,"Sea Terror Notes","None", 0),
            (9832,"Goldforged Compass","None", 0),
            (9833,"Silver Key Bundle","None", 0),
            (9834,"Sea Terror Jerky","None", 0),
            (9835,"Brilliant Lamplight","Combustion", 0),
            (9836,"Aged Sheet Music","Sinking", 0),
            (9837,"Metal Construct","Burst", 0),
            (9838,"Moonmirror Wine Cup","Breath", 0),
            (9839,"Classical-design Letter Opener","Vibration", 0),
            (9840,"W Corp. Standard Issue Cap","Charge", 0),
            (9841,"Cleanup Agent Gear Set C","Charge", 0),
            (9842,"Bloody Flesh, Fleshy Blood","Laceration", 0),
            (9843,"Hardblood Glaive","None", 0)
        };

        public InjectorUI(IntPtr ptr) : base(ptr) { }

        private void Start()
        {
            try
            {
                if (File.Exists(LetheGiftInjectorPlugin.TokenFilePath))
                {
                    _token = File.ReadAllText(LetheGiftInjectorPlugin.TokenFilePath).Trim();
                    _status = "Saved token loaded. Press Fetch State.";
                    LetheGiftInjectorPlugin.Log?.LogInfo("Saved token loaded successfully.");
                }
            }
            catch (Exception ex)
            {
                LetheGiftInjectorPlugin.Log?.LogWarning("Failed to load token: " + ex.Message);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Backslash))
                _showPanel = !_showPanel;

            if (_pendingTask != null && _pendingTask.IsCompleted)
            {
                if (_pendingTask.IsFaulted)
                    _status = "Error: " + (_pendingTask.Exception?.InnerException?.Message ?? "Unknown");
                _pendingTask = null;
            }
        }

        private void OnGUI()
        {
            if (!_showPanel) return;

            GUI.Box(_windowRect, "Lethe Gift Injector ver.2.9.2");

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
            if (GUILayout.Button("Save", GUILayout.Width(50))) SaveToken();
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

            GUILayout.Label(isBusy ? "[Processing...]" : _status);
            GUILayout.Label($"Queue: {_pendingGifts.Count} item(s)");

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label("ID:", GUILayout.Width(20));
            _inputId   = GUILayout.TextField(_inputId,   GUILayout.Width(80));
            GUILayout.Label("Tier:", GUILayout.Width(32));
            _inputTier = GUILayout.TextField(_inputTier, GUILayout.Width(28));
            if (GUILayout.Button("Add", GUILayout.Width(55))) TryAdd();
            if (GUILayout.Button("Clear", GUILayout.Width(55)))
            {
                _pendingGifts.Clear();
                _status = "Queue cleared.";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // ── Search field ─────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(48));
            string newQuery = GUILayout.TextField(_searchQuery, GUILayout.Width(340));
            if (newQuery != _searchQuery)
            {
                _searchQuery = newQuery;
                _giftPage = 0;
            }
            if (GUILayout.Button("X", GUILayout.Width(28)))
            {
                _searchQuery = "";
                _giftPage = 0;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // ── Keyword filter buttons ────────────────────────────────────────
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _kwRow1.Length; i++)
            {
                int idx = i;
                if (GUILayout.Toggle(_kwIndex == idx, _kwRow1[i], "Button", GUILayout.Width(82)))
                {
                    if (_kwIndex != idx) { _kwIndex = idx; _giftPage = 0; }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int i = 0; i < _kwRow2.Length; i++)
            {
                int idx = i + _kwRow1.Length;
                if (GUILayout.Toggle(_kwIndex == idx, _kwRow2[i], "Button", GUILayout.Width(82)))
                {
                    if (_kwIndex != idx) { _kwIndex = idx; _giftPage = 0; }
                }
            }
            GUILayout.EndHorizontal();

            // ── Build filtered list (keyword + search query) ──────────────────
            string selKw = _allKeywords[_kwIndex];
            string queryLower = _searchQuery.Trim().ToLowerInvariant();
            var filtered = new System.Collections.Generic.List<(int gid, string name, string kw, int maxTier)>();
            foreach (var g in _allGifts)
            {
                bool kwMatch = selKw == "All" || g.kw == selKw;
                bool searchMatch = string.IsNullOrEmpty(queryLower)
                    || g.name.ToLowerInvariant().Contains(queryLower)
                    || g.gid.ToString().Contains(queryLower);
                if (kwMatch && searchMatch) filtered.Add(g);
            }

            if (GUILayout.Button($"Add All Filtered ({filtered.Count})", GUILayout.Width(240)))
                AddAllFiltered(filtered);

            GUILayout.Space(2);

            int giftStart = _giftPage * PAGE_SIZE;
            int giftEnd   = Math.Min(giftStart + PAGE_SIZE, filtered.Count);
            for (int i = giftStart; i < giftEnd; i++)
            {
                var g = filtered[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(g.name, GUILayout.Width(450));
                // Automatically inject the appropriate maxTier for the item
                if (GUILayout.Button("+", GUILayout.Width(28))) AddToList(g.gid, g.maxTier); 
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
                _status = "Token saved.";
            }
            catch (Exception ex)
            {
                _status = "Failed to save token: " + ex.Message;
            }
        }

        private async Task FetchStateAsync()
        {
            _status = "Fetching...";
            _fetched = false;
            try
            {
                var reqBody  = $"{{\"token\":\"{EscapeJson(_token.Trim())}\"}}";
                var content  = new StringContent(reqBody, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(FETCH_URL, content);
                var respStr  = await response.Content.ReadAsStringAsync();

                LetheGiftInjectorPlugin.Log?.LogInfo(
                    $"Fetch response ({(int)response.StatusCode}): {respStr}");

                if (!response.IsSuccessStatusCode)
                {
                    _status = $"Fetch failed ({(int)response.StatusCode}) — check logs";
                    return;
                }

                _state = JsonNode.Parse(respStr);

                var currentInfo = _state?["currentInfo"];
                if (currentInfo == null)
                {
                    _status = "[Error] 'currentInfo' not found in response — check logs";
                    LetheGiftInjectorPlugin.Log?.LogError(
                        "FetchStateAsync: currentInfo is null.");
                    return;
                }

                var egsNode = currentInfo["egs"];
                if (egsNode == null)
                {
                    LetheGiftInjectorPlugin.Log?.LogWarning(
                        "FetchStateAsync: 'egs' key missing. currentInfo: "
                        + currentInfo.ToJsonString());
                    _status = "[Warning] 'egs' key missing — check logs";
                    _fetched = true;
                    return;
                }

                _egsKey  = "egs";
                _fetched = true;
                _status  = $"Fetch successful. Current gifts: {egsNode.AsArray().Count}";
            }
            catch (Exception ex)
            {
                _status = "Fetch error: " + ex.Message;
                LetheGiftInjectorPlugin.Log?.LogError("Fetch error: " + ex);
            }
        }

        private async Task UpdateStateAsync()
        {
            if (_state == null) { _status = "[Error] No state. Run Fetch first."; return; }
            _status = "Updating...";
            try
            {
                var currentInfo = _state["currentInfo"];
                if (currentInfo == null)
                {
                    _status = "[Error] currentInfo missing — check logs";
                    LetheGiftInjectorPlugin.Log?.LogError(
                        "UpdateStateAsync: currentInfo is null.");
                    return;
                }

                var egsNode = currentInfo[_egsKey];
                if (egsNode == null)
                {
                    _status = $"[Error] '{_egsKey}' key missing — check logs";
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
                    $"Update response ({(int)response.StatusCode}): {respStr}");

                if (!response.IsSuccessStatusCode)
                {
                    _status = $"Update failed ({(int)response.StatusCode}) — check logs";
                    return;
                }

                _status = $"Update successful. {added} gift(s) added. Takes effect on dungeon entry.";
                _pendingGifts.Clear();
            }
            catch (Exception ex)
            {
                _status = "Update error: " + ex.Message;
                LetheGiftInjectorPlugin.Log?.LogError("Update error: " + ex);
            }
        }

        private void TryAdd()
        {
            if (!int.TryParse(_inputId, out int id))
            { _status = "[Error] Invalid number"; return; }
            
            // For manual input, parse the _inputTier field (defaults to 0 safely)
            if (!int.TryParse(_inputTier, out int tier) || tier < 0 || tier > 2) tier = 0; 
            AddToList(id, tier);
        }

        private void AddToList(int id, int ul)
        {
            foreach (var (eid, _) in _pendingGifts)
                if (eid == id) { _status = $"ID {id} already in queue"; return; }
            _pendingGifts.Add((id, ul));
            _status = $"ID {id} (Tier {ul}) added";
        }

        private void AddAllFiltered(
            System.Collections.Generic.List<(int gid, string name, string kw, int maxTier)> filtered)
        {
            int added = 0;
            foreach (var g in filtered)
            {
                bool exists = false;
                foreach (var (eid, _) in _pendingGifts)
                    if (eid == g.gid) { exists = true; break; }
                // Pull maxTier directly from the item's array data
                if (!exists) { _pendingGifts.Add((g.gid, g.maxTier)); added++; }
            }
            _status = $"{added} gift(s) added from filter (duplicates excluded)";
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}