using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class JungleQuestGame : MonoBehaviour
{
    const int CurrentSceneContentVersion = 11;
    const string CharacterPreferenceKey = "JungleAdventure.SelectedCharacter";
    enum MenuPage { Main, Characters, HowToPlay, Pause, GameOver, Victory, Hidden }
    public static JungleQuestGame I;
    const float EndX = 180f;
    const float JumpVelocity = 14.3f;
    const float MinimumJumpVelocity = 9.8f;
    readonly Color leaf = new Color(.12f,.48f,.28f), deep = new Color(.035f,.18f,.17f), gold = new Color(1f,.72f,.12f);
    GameObject player, overlay, playButton, restartButton, quitButton, resumeButton;
    Rigidbody2D body; Collider2D bodyCollider; Camera cam;
    [SerializeField] Camera sceneCamera;
    [SerializeField] GameObject scenePlayer;
    [SerializeField] GameObject sceneOverlay, scenePlayButton, sceneRestartButton, sceneQuitButton, sceneResumeButton;
    [SerializeField] Text sceneHud, sceneTitleText, sceneMessageText;
    [SerializeField] GameObject sceneCharacterPicker, sceneSoundButton, scenePauseButton;
    [SerializeField] GameObject sceneMainMenuPage, sceneHowToPlayPage, scenePausePage, sceneEndPage, scenePauseRestartButton;
    [SerializeField] GameObject sceneCharacterOpenButton, sceneHowToPlayButton, sceneCharacterSelectButton, sceneCharacterBackButton;
    [SerializeField] GameObject sceneCharacterPreviousButton, sceneCharacterNextButton, sceneHowToPlayBackButton, scenePauseMainMenuButton, sceneEndMainMenuButton;
    [SerializeField] GameObject[] sceneCharacterButtons;
    [SerializeField] GameObject[] sceneCoinSparkles;
    [SerializeField] bool sceneDefaultsAuthored;
    [SerializeField] int sceneContentVersion;
    Text hud, titleText, messageText; int coins, lives=3, score; bool playing, dead, won, grounded; float checkpointX=2, coyote, jumpBuffer;
    float speedPowerupUntil, magnetPowerupUntil;
    readonly List<GameObject> coinObjects = new List<GameObject>(); int totalCoins;
    readonly List<Vector3> cameraBounds = new List<Vector3>();
    readonly struct SpriteCacheKey : System.IEquatable<SpriteCacheKey>
    {
        readonly Color color;
        readonly int shape;
        public SpriteCacheKey(Color color, int shape) { this.color = color; this.shape = shape; }
        public bool Equals(SpriteCacheKey other) => color.Equals(other.color) && shape == other.shape;
        public override bool Equals(object obj) => obj is SpriteCacheKey other && Equals(other);
        public override int GetHashCode() { unchecked { return color.GetHashCode() * 397 ^ shape; } }
    }
    static readonly Dictionary<SpriteCacheKey, Sprite> spriteCache = new Dictionary<SpriteCacheKey, Sprite>();
    static Sprite fallenLogArtwork;
    static readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
    static Texture2D cachedExplorerSheet;static Sprite[] cachedExplorerFrames;
    static readonly Sprite[][] cachedCharacterFrames = new Sprite[4][];
    static readonly string[] CharacterNames = { "MAYA EXPLORER", "KOA WARRIOR", "TAVI SCOUT", "NIA ADVENTURER" };
    static readonly Color[] CharacterAccents = { new Color(.27f,.63f,.39f), new Color(.25f,.61f,.77f), new Color(.79f,.43f,.68f), new Color(.91f,.65f,.19f) };
    static ContactFilter2D groundFilter;
    Vector3 cameraVelocity;float cameraShake;
    int hudCoins=-1,hudLives=-1,hudCheckpoint=-1,hudScore=-1; string hudPowerups="";
    Sprite platformSprite;
    JungleAudio audio;
    int selectedCharacter, pendingCharacter;
    MenuPage menuPage=MenuPage.Hidden;
    static bool startAfterReload;

    void Awake(){
        if(I&&I!=this){enabled=false;return;} I=this;Time.timeScale=1;Application.targetFrameRate=60;groundFilter=new ContactFilter2D();groundFilter.SetLayerMask(Physics2D.DefaultRaycastLayers);groundFilter.useTriggers=false;
        selectedCharacter=Mathf.Clamp(PlayerPrefs.GetInt(CharacterPreferenceKey,0),0,CharacterNames.Length-1);pendingCharacter=selectedCharacter;
        if(!sceneDefaultsAuthored||!sceneCamera||!scenePlayer||!sceneOverlay){Debug.LogError("Jungle Quest scene defaults are missing. Use Jungle Quest > Bake All Default Objects Into Scene in Edit Mode.");enabled=false;return;}
        BindSceneWorld();BindSceneUI();audio=GetComponent<JungleAudio>();if(!audio)audio=gameObject.AddComponent<JungleAudio>();audio.Initialize(cam);ShowStart();if(startAfterReload){startAfterReload=false;StartGame();}
    }
    void OnDestroy(){if(I==this)I=null;}
    public bool HasAuthoredSceneDefaults=>sceneDefaultsAuthored;
    public bool NeedsSceneRebuild=>sceneContentVersion<CurrentSceneContentVersion;
    public bool IsPlaying=>playing;
    public Transform PlayerTransform=>player?player.transform:null;
    public void SetSceneWorld(Camera authoredCamera,GameObject authoredPlayer){sceneCamera=authoredCamera;scenePlayer=authoredPlayer;}
    public bool AuthorDefaultSceneObjectsForEditor(){
        if(Application.isPlaying||sceneDefaultsAuthored)return sceneDefaultsAuthored;
        if(!sceneCamera||!scenePlayer){Debug.LogError("Create the Edit Mode world camera and player before baking game defaults.");return false;}
        groundFilter=new ContactFilter2D();groundFilter.SetLayerMask(Physics2D.DefaultRaycastLayers);groundFilter.useTriggers=false;
        BuildWorld();BuildUI();BuildCoinSparklePool();sceneDefaultsAuthored=true;sceneContentVersion=CurrentSceneContentVersion;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        return true;
    }
    void BindSceneWorld(){
        cam=sceneCamera;player=scenePlayer;bodyCollider=player.GetComponent<Collider2D>();body=player.GetComponent<Rigidbody2D>();ConfigureSceneExplorer(player,selectedCharacter);
        coinObjects.Clear();var authoredCoins=FindObjectsByType<JungleCoin>();foreach(var coin in authoredCoins)if(coin.gameObject.scene==gameObject.scene)coinObjects.Add(coin.gameObject);totalCoins=coinObjects.Count;
    }
    void BindSceneUI(){
        overlay=sceneOverlay;playButton=scenePlayButton;restartButton=sceneRestartButton;quitButton=sceneQuitButton;resumeButton=sceneResumeButton;hud=sceneHud;titleText=sceneTitleText;messageText=sceneMessageText;
        WireButton(playButton,StartGame);WireButton(restartButton,Restart);WireButton(scenePauseRestartButton,Restart);WireButton(quitButton,ExitGame);WireButton(resumeButton,Resume);
        WireButton(sceneCharacterOpenButton,ShowCharacterSelection);WireButton(sceneHowToPlayButton,ShowHowToPlay);WireButton(scenePauseButton,PauseGame);
        WireButton(sceneCharacterSelectButton,CommitCharacterSelection);WireButton(sceneCharacterBackButton,ShowStart);
        WireButton(sceneCharacterPreviousButton,()=>CycleCharacter(-1));WireButton(sceneCharacterNextButton,()=>CycleCharacter(1));
        WireButton(sceneHowToPlayBackButton,ShowStart);WireButton(scenePauseMainMenuButton,ReturnToMainMenu);WireButton(sceneEndMainMenuButton,ReturnToMainMenu);
        if(sceneCharacterButtons!=null)for(int i=0;i<sceneCharacterButtons.Length;i++){int index=i;WireButton(sceneCharacterButtons[i],()=>PreviewCharacter(index));}
        WireButton(sceneSoundButton,ToggleSound);
        UpdateCharacterCards();UpdateSoundButton();
    }
    void WireButton(GameObject target,UnityEngine.Events.UnityAction action){if(!target)return;var button=target.GetComponent<Button>();if(!button)return;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{audio?.PlayButtonClick();action();});}
    Sprite MakeSprite(Color c, int shape=0){
        var key=new SpriteCacheKey(c,shape);if(spriteCache.TryGetValue(key,out var cached))return cached;
        const int n=128;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;var px=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=(x+.5f-n/2f)/(n/2f),dy=(y+.5f-n/2f)/(n/2f);float edge;
            if(shape==1)edge=(Mathf.Abs(dx)+Mathf.Abs(dy)-.88f)*.7f;
            else if(shape==2)edge=(dx*dx+dy*dy*2.25f-.88f)*.55f;
            else if(shape==3)edge=(Mathf.Abs(dy)-Mathf.Pow(Mathf.Max(0,1-Mathf.Abs(dx)),.72f)*.72f)*.8f;
            else if(shape==4){float qx=Mathf.Abs(dx)-.76f,qy=Mathf.Abs(dy)-.72f;edge=(Mathf.Sqrt(Mathf.Max(qx,0)*Mathf.Max(qx,0)+Mathf.Max(qy,0)*Mathf.Max(qy,0))+Mathf.Min(Mathf.Max(qx,qy),0)-.12f)*.55f;}
            else if(shape==5)edge=(Mathf.Abs(dx)*.76f+dy*.65f-.62f)*.7f;
            else if(shape==6){float triangleY=Mathf.InverseLerp(-.82f,.88f,dy);float halfWidth=Mathf.Lerp(.72f,.035f,triangleY);edge=Mathf.Max(Mathf.Abs(dx)-halfWidth,Mathf.Max(-.82f-dy,dy-.88f))*.7f;}
            else edge=(dx*dx+dy*dy-.84f)*.6f;
            float alpha=1-Mathf.SmoothStep(-.025f,.025f,edge);Color p=c;p.a*=alpha;
            if(alpha>0&&shape==1&&Mathf.Abs(dx)<.1f){p=Color.Lerp(p,Color.white,.48f*(1-Mathf.Abs(dx)*8));}
            px[y*n+x]=p;
        }
        t.SetPixels(px);t.Apply();cached=Sprite.Create(t,new Rect(0,0,n,n),new Vector2(.5f,.5f),128);spriteCache[key]=cached;return cached;
    }
    Sprite MakeFallenLogArtwork(){
        if(fallenLogArtwork)return fallenLogArtwork;
        fallenLogArtwork=Resources.Load<Sprite>("Artwork/FallenLogArt");
        if(fallenLogArtwork)return fallenLogArtwork;
        const int w=512,h=224;var texture=new Texture2D(w,h,TextureFormat.RGBA32,false);texture.name="Fallen jungle tree log";texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;var pixels=new Color[w*h];
        PaintEllipse(pixels,w,h,new Vector2(256,40),222,27,new Color(.025f,.045f,.018f,.42f));
        PaintSegment(pixels,w,h,new Vector2(34,99),new Vector2(478,101),47,new Color(.11f,.055f,.025f));
        PaintSegment(pixels,w,h,new Vector2(37,102),new Vector2(476,104),40,new Color(.34f,.17f,.065f));
        PaintSegment(pixels,w,h,new Vector2(64,126),new Vector2(431,128),10,new Color(.47f,.265f,.105f));
        PaintSegment(pixels,w,h,new Vector2(78,135),new Vector2(412,137),3,new Color(.62f,.38f,.17f,.8f));
        // Dark, irregular bark grooves give the trunk a readable rounded surface.
        for(int i=0;i<9;i++){
            float x=82+i*43f;float y=91+Mathf.Sin(i*1.71f)*9f;float length=20+(i%3)*8;
            PaintSegment(pixels,w,h,new Vector2(x,y),new Vector2(x+length,y+Mathf.Sin(i*2.2f)*5f),2.6f,new Color(.16f,.075f,.03f,.78f));
            PaintSegment(pixels,w,h,new Vector2(x+8,y+5),new Vector2(x+length+3,y+7),1.1f,new Color(.57f,.32f,.12f,.72f));
        }
        for(int knot=0;knot<3;knot++){
            float x=154+knot*105f;float y=99+Mathf.Sin(knot*2.3f)*7f;
            PaintEllipse(pixels,w,h,new Vector2(x,y),14,9,new Color(.18f,.075f,.03f,.82f));
            PaintEllipse(pixels,w,h,new Vector2(x+1,y+1),7,4,new Color(.49f,.25f,.09f,.92f));
        }
        // Cut ends and their growth rings make the fallen trunk recognizable at game scale.
        var leftEnd=new Vector2(52,102);var rightEnd=new Vector2(461,104);
        PaintEllipse(pixels,w,h,leftEnd,43,41,new Color(.13f,.065f,.03f));
        PaintEllipse(pixels,w,h,leftEnd,34,32,new Color(.67f,.43f,.22f));
        PaintEllipseRing(pixels,w,h,leftEnd,26,25,2.2f,new Color(.31f,.14f,.055f));
        PaintEllipseRing(pixels,w,h,leftEnd,17,17,1.7f,new Color(.43f,.21f,.08f));
        PaintEllipse(pixels,w,h,leftEnd,7,7,new Color(.29f,.12f,.045f));
        PaintEllipse(pixels,w,h,rightEnd,43,41,new Color(.13f,.065f,.03f));
        PaintEllipse(pixels,w,h,rightEnd,34,32,new Color(.72f,.49f,.27f));
        PaintEllipseRing(pixels,w,h,rightEnd,27,25,2.4f,new Color(.31f,.14f,.055f));
        PaintEllipseRing(pixels,w,h,rightEnd,17,16,1.8f,new Color(.45f,.22f,.08f));
        PaintEllipse(pixels,w,h,rightEnd,7,7,new Color(.29f,.12f,.045f));
        // Broken branch stubs and small moss clusters add a natural silhouette.
        PaintSegment(pixels,w,h,new Vector2(135,130),new Vector2(119,178),12,new Color(.2f,.095f,.035f));
        PaintSegment(pixels,w,h,new Vector2(128,147),new Vector2(119,177),7,new Color(.48f,.25f,.09f));
        PaintSegment(pixels,w,h,new Vector2(352,131),new Vector2(370,172),11,new Color(.2f,.095f,.035f));
        PaintSegment(pixels,w,h,new Vector2(359,148),new Vector2(370,171),6,new Color(.5f,.28f,.1f));
        for(int i=0;i<8;i++){
            float x=102+i*44f;float y=139+Mathf.Sin(i*1.53f)*4f;
            PaintLeaf(pixels,w,h,new Vector2(x,y),1.2f+(i%3)*.28f,24+(i%3)*5,7,new Color(.18f,.37f,.1f));
            if(i%2==0)PaintLeaf(pixels,w,h,new Vector2(x+4,y-2),2.0f,19,6,new Color(.29f,.48f,.13f));
        }
        texture.SetPixels(pixels);texture.Apply(false,false);fallenLogArtwork=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),256,0,SpriteMeshType.FullRect);fallenLogArtwork.name="Mossy fallen jungle log";return fallenLogArtwork;
    }
    static void BlendPixel(Color[] pixels,int w,int h,int x,int y,Color color,float coverage){
        if(x<0||x>=w||y<0||y>=h||coverage<=0)return;int index=y*w+x;Color dst=pixels[index];float srcA=Mathf.Clamp01(color.a*coverage),outA=srcA+dst.a*(1-srcA);if(outA<=0)return;float remain=dst.a*(1-srcA);
        pixels[index]=new Color((color.r*srcA+dst.r*remain)/outA,(color.g*srcA+dst.g*remain)/outA,(color.b*srcA+dst.b*remain)/outA,outA);
    }
    static void PaintEllipse(Color[] pixels,int w,int h,Vector2 center,float rx,float ry,Color color){
        int minX=Mathf.Max(0,Mathf.FloorToInt(center.x-rx-2)),maxX=Mathf.Min(w-1,Mathf.CeilToInt(center.x+rx+2));int minY=Mathf.Max(0,Mathf.FloorToInt(center.y-ry-2)),maxY=Mathf.Min(h-1,Mathf.CeilToInt(center.y+ry+2));
        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++){float dx=(x+.5f-center.x)/rx,dy=(y+.5f-center.y)/ry,d=Mathf.Sqrt(dx*dx+dy*dy);float cover=1-Mathf.SmoothStep(.92f,1.08f,d);float grain=.92f+.12f*Mathf.PerlinNoise(x*.055f+1,y*.08f+4);Color shaded=color*grain;BlendPixel(pixels,w,h,x,y,shaded,cover);}
    }
    static void PaintSegment(Color[] pixels,int w,int h,Vector2 a,Vector2 b,float radius,Color color){
        int minX=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,b.x)-radius-2)),maxX=Mathf.Min(w-1,Mathf.CeilToInt(Mathf.Max(a.x,b.x)+radius+2));int minY=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,b.y)-radius-2)),maxY=Mathf.Min(h-1,Mathf.CeilToInt(Mathf.Max(a.y,b.y)+radius+2));Vector2 ab=b-a;float lengthSq=Mathf.Max(.001f,ab.sqrMagnitude);
        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++){Vector2 p=new Vector2(x+.5f,y+.5f);float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/lengthSq);float d=Vector2.Distance(p,a+ab*t);float cover=1-Mathf.SmoothStep(radius-.8f,radius+.8f,d);float grain=.84f+.22f*Mathf.PerlinNoise(x*.09f+2,y*.07f+7);BlendPixel(pixels,w,h,x,y,color*grain,cover);}
    }
    static void PaintEllipseRing(Color[] pixels,int w,int h,Vector2 center,float rx,float ry,float thickness,Color color){
        const int steps=64;Vector2 previous=center+new Vector2(rx,0);for(int i=1;i<=steps;i++){float angle=i*Mathf.PI*2f/steps;Vector2 next=center+new Vector2(Mathf.Cos(angle)*rx,Mathf.Sin(angle)*ry);PaintSegment(pixels,w,h,previous,next,thickness,color);previous=next;}
    }
    static void PaintThorn(Color[] pixels,int w,int h,Vector2 root,float length,float lean,float halfWidth,Color color){
        float tipY=root.y+length,tipX=root.x+lean;int minX=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(root.x-halfWidth,tipX)-2)),maxX=Mathf.Min(w-1,Mathf.CeilToInt(Mathf.Max(root.x+halfWidth,tipX)+2));int minY=Mathf.Max(0,Mathf.FloorToInt(root.y-1)),maxY=Mathf.Min(h-1,Mathf.CeilToInt(tipY+1));
        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++){float t=Mathf.Clamp01((y+.5f-root.y)/length);if(y+.5f<root.y||y+.5f>tipY)continue;float half=Mathf.Lerp(halfWidth,.4f,t);float center=Mathf.Lerp(root.x,tipX,t);float d=Mathf.Abs(x+.5f-center)-half;float cover=1-Mathf.SmoothStep(-.7f,.7f,d);float side=(x+.5f-center)/Mathf.Max(.1f,half);float shade=.78f+.17f*(1-t)+.15f*Mathf.Clamp01(-side);BlendPixel(pixels,w,h,x,y,color*shade,cover);}
    }
    static void PaintLeaf(Color[] pixels,int w,int h,Vector2 root,float angle,float length,float halfWidth,Color color){
        Vector2 axis=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),perp=new Vector2(-axis.y,axis.x),tip=root+axis*length;int extent=Mathf.CeilToInt(length+halfWidth+2);int minX=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(root.x,tip.x)-extent)),maxX=Mathf.Min(w-1,Mathf.CeilToInt(Mathf.Max(root.x,tip.x)+extent));int minY=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(root.y,tip.y)-extent)),maxY=Mathf.Min(h-1,Mathf.CeilToInt(Mathf.Max(root.y,tip.y)+extent));
        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++){Vector2 d=new Vector2(x+.5f-root.x,y+.5f-root.y);float along=Vector2.Dot(d,axis)/length;if(along<=0||along>=1)continue;float across=Vector2.Dot(d,perp),width=halfWidth*Mathf.Sin(Mathf.PI*along);float cover=1-Mathf.SmoothStep(width-.8f,width+.8f,Mathf.Abs(across));float grain=.78f+.22f*Mathf.PerlinNoise(x*.075f+5,y*.075f+3);float vein=1-Mathf.SmoothStep(1.1f,2.5f,Mathf.Abs(across));BlendPixel(pixels,w,h,x,y,color*grain,cover);if(vein>0)BlendPixel(pixels,w,h,x,y,new Color(.49f,.57f,.19f,color.a),vein*cover*.62f);}
    }
    GameObject Shape(string name, Vector2 pos, Vector2 size, Color color, int z=0, int style=0){var g=new GameObject(name);g.transform.position=new Vector3(pos.x,pos.y,z);g.transform.localScale=size;var sr=g.AddComponent<SpriteRenderer>();sr.sprite=MakeSprite(color,style);return g;}
    GameObject Detail(Transform parent,string name,Vector2 at,Vector2 size,Color color,int z=0,int style=0,float rotation=0){var g=Shape(name,Vector2.zero,size,color,z,style);g.transform.SetParent(parent,false);g.transform.localPosition=new Vector3(at.x,at.y,z);g.transform.localRotation=Quaternion.Euler(0,0,rotation);return g;}
    void Solid(float x,float y,float w,float h){var g=new GameObject("Jungle earth platform");g.transform.position=new Vector3(x,y,0);var c=g.AddComponent<BoxCollider2D>();c.size=new Vector2(w,h);if(platformSprite){var artwork=new GameObject("Layered soil and moss");artwork.transform.SetParent(g.transform,false);artwork.transform.localPosition=new Vector3(0,h*.0625f,.05f);artwork.transform.localScale=new Vector3(w/platformSprite.bounds.size.x,h/(platformSprite.bounds.size.y*.64f),1);var sr=artwork.AddComponent<SpriteRenderer>();sr.sprite=platformSprite;sr.sortingOrder=-1;}
        else {Shape("Moss shelf",new Vector2(x,y+h*.4f),new Vector2(w,.27f),new Color(.29f,.68f,.31f),0,4);Shape("Sunlit grass lip",new Vector2(x,y+h*.49f),new Vector2(w,.09f),new Color(.63f,.78f,.28f),0,4);}}
    void BuildWorld(){
        bool useSceneWorld=sceneCamera&&scenePlayer;
        if(!useSceneWorld){Debug.LogError("World construction is editor-only; author the scene camera and player first.");return;}
        cam=sceneCamera;
        cam.orthographic=true;cam.orthographicSize=6.1f;cam.transform.position=new Vector3(0,0,-10);cam.backgroundColor=new Color(.34f,.73f,.78f);
        platformSprite=Resources.Load<Sprite>("JungleGround");if(!platformSprite){var groundSprites=Resources.LoadAll<Sprite>("JungleGround");if(groundSprites.Length>0)platformSprite=groundSprites[0];}
        float[] cx={5,8,11,15,18,22,28,32,35,39,43,48,52,57,61,65,71,76,82,87,91,97,103,108,114,119,123,127,132,137,142,147,152,157,162,167,172,177,179};
        foreach(float x in cx){float y=(x%4==0?-.4f:-2.15f);coinObjects.Add(BuildCoin(new Vector2(x,y)));} totalCoins=coinObjects.Count;
        BuildPowerup(new Vector2(13,-.55f),JunglePowerupKind.Speed);
        BuildPowerup(new Vector2(42,-.55f),JunglePowerupKind.Magnet);
        BuildPowerup(new Vector2(105,-.55f),JunglePowerupKind.Magnet);
        BuildPowerup(new Vector2(145,-.55f),JunglePowerupKind.Speed);
        MakeFallenLog(25,-3.15f,3.2f);MakeFallenLog(45,-3.1f,3.2f);MakeFallenLog(67,-3.15f,3.2f);MakeFallenLog(97,-3.1f,3.2f);MakeFallenLog(126,-3.1f,3.2f);MakeFallenLog(147,-3.1f,3.3f);MakeFallenLog(169,-3.1f,3.4f);
        MakeThornGrassHazard(36,-1.75f,1f);
        MakeTreeStump(135,-3.15f,1f);
        AddPatrollingBeetle(new Vector2(54,-2.7f),51,59);AddPatrollingBeetle(new Vector2(151,-2.7f),149,155);
        AddPatrollingOwl(new Vector2(84,1.65f),78,90);AddPatrollingOwl(new Vector2(166,1.7f),158,175);
        BuildCheckpoint(70);BuildCheckpoint(141);
        var goal=BuildTempleExit(new Vector2(176,-1.4f));var goalTrigger=goal.AddComponent<BoxCollider2D>();goalTrigger.size=new Vector2(3.2f,4.2f);goalTrigger.offset=new Vector2(0,-.9f);goalTrigger.isTrigger=true;goal.AddComponent<JungleFinish>();
        // Keep foreground foliage at the edges so it frames the course without hiding hazards or feet.
        for(int i=0;i<23;i++){float x=i*8.2f-4;BuildFern(new Vector2(x,-4.25f),i%2==0?new Color(.12f,.45f,.22f):new Color(.2f,.58f,.27f),-2,.56f);}
        BuildAtmosphere();
        BuildEnvironmentDetails();
        player=scenePlayer;ConfigureSceneExplorer(player);var boxCollider=player.GetComponent<BoxCollider2D>();if(!boxCollider&&!player.GetComponent<Collider2D>())boxCollider=player.AddComponent<BoxCollider2D>();if(boxCollider){boxCollider.size=new Vector2(.72f,1.28f);bodyCollider=boxCollider;}else bodyCollider=player.GetComponent<Collider2D>();
        body=player.GetComponent<Rigidbody2D>();if(!body)body=player.AddComponent<Rigidbody2D>();body.freezeRotation=true;body.gravityScale=3.2f;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;body.interpolation=RigidbodyInterpolation2D.Interpolate;
        // Camera boundaries use the fixed course limits.
    }
    public void ConfigureSceneExplorer(GameObject target,int character=0){
        var visual=target.GetComponent<JunglePlayerVisual>();if(!visual){Debug.LogError("The saved Explorer is missing JunglePlayerVisual.");return;}var renderer=target.GetComponentInChildren<SpriteRenderer>();if(!renderer)return;
        var frames=GetCharacterFrames(character);bool haveFrames=frames!=null&&frames.Length==9;
        if(haveFrames){float fw=frames[0].texture.width,fh=frames[0].texture.height;var art=renderer.transform;art.localPosition=new Vector3(0,-.58f,-.2f);art.localScale=new Vector3(1.52f/(fw/512f),1.72f/(fh/512f),1);renderer.sprite=frames[4];renderer.sortingOrder=50;visual.SetSpriteAnimation(renderer,new[]{frames[0],frames[1],frames[2],frames[3]},frames[4],frames[5],frames[6],frames[7],frames[8]);return;}
        Debug.LogError($"Character {character} is missing its 9 imported animation frames.");
    }
    static Sprite[] GetCharacterFrames(int character){
        character=Mathf.Clamp(character,0,3);if(cachedCharacterFrames[character]!=null)return cachedCharacterFrames[character];
        string name=character==0?"Maya":(character==1?"Koa":(character==2?"Tavi":"Nia"));string prefix="CharacterFrames/"+name+"Frame";var frames=new Sprite[9];
        for(int i=0;i<frames.Length;i++)frames[i]=Resources.Load<Sprite>(prefix+i);
        for(int i=0;i<frames.Length;i++)if(!frames[i])return null;
        cachedCharacterFrames[character]=frames;return frames;
    }
    void PreviewCharacter(int character){pendingCharacter=Mathf.Clamp(character,0,CharacterNames.Length-1);UpdateCharacterCards();UpdateCharacterPreviewMessage();audio?.PlaySelection();}
    void CycleCharacter(int direction){PreviewCharacter((pendingCharacter+direction+CharacterNames.Length)%CharacterNames.Length);}
    void CommitCharacterSelection(){selectedCharacter=pendingCharacter;PlayerPrefs.SetInt(CharacterPreferenceKey,selectedCharacter);PlayerPrefs.Save();if(player){var visual=player.GetComponent<JunglePlayerVisual>();var frames=GetCharacterFrames(selectedCharacter);if(visual&&frames!=null)visual.SetCharacterFrames(frames);}ShowStart();}
    void UpdateCharacterPreviewMessage(){SetMenuMessage($"{CharacterNames[pendingCharacter]} selected\nPreview with the cards or arrows. Select to save.",new Vector2(.5f,.64f),new Vector2(860,84),20);}
    void UpdateCharacterCards(){
        if(sceneCharacterButtons==null)return;for(int i=0;i<sceneCharacterButtons.Length;i++){
            var card=sceneCharacterButtons[i];if(!card)continue;var image=card.GetComponent<Image>();if(image)image.color=i==pendingCharacter?new Color(CharacterAccents[i].r*.72f,CharacterAccents[i].g*.72f,CharacterAccents[i].b*.72f,1):new Color(.07f,.22f,.19f,.97f);
            var label=card.GetComponentInChildren<Text>();if(label)label.text=(i==pendingCharacter?"SELECTED\n":"")+CharacterNames[i];
            var outline=card.GetComponent<Outline>();if(outline){outline.effectColor=i==pendingCharacter?new Color(1f,.78f,.3f,.95f):new Color(.56f,.7f,.55f,.48f);outline.effectDistance=i==pendingCharacter?new Vector2(2,-2):new Vector2(1,-1);}
            var icon=card.transform.Find("Character portrait")?.GetComponent<Image>();var frames=GetCharacterFrames(i);if(icon&&frames!=null){icon.sprite=frames[4];icon.color=Color.white;}
        }
    }
    void SetMenuPage(MenuPage page){menuPage=page;if(overlay)overlay.SetActive(page!=MenuPage.Hidden);if(sceneMainMenuPage)sceneMainMenuPage.SetActive(page==MenuPage.Main);if(sceneCharacterPicker)sceneCharacterPicker.SetActive(page==MenuPage.Characters);if(sceneHowToPlayPage)sceneHowToPlayPage.SetActive(page==MenuPage.HowToPlay);if(scenePausePage)scenePausePage.SetActive(page==MenuPage.Pause);if(sceneEndPage)sceneEndPage.SetActive(page==MenuPage.GameOver||page==MenuPage.Victory);if(scenePauseButton)scenePauseButton.SetActive(page==MenuPage.Hidden&&playing);}
    void ShowCharacterSelection(){pendingCharacter=selectedCharacter;titleText.text="CHOOSE YOUR CHARACTER";UpdateCharacterCards();UpdateCharacterPreviewMessage();SetMenuPage(MenuPage.Characters);UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(sceneCharacterButtons!=null&&sceneCharacterButtons.Length>selectedCharacter?sceneCharacterButtons[selectedCharacter]:sceneCharacterSelectButton);}
    void ShowHowToPlay(){titleText.text="HOW TO PLAY";SetMenuMessage("MOVE: A / D OR LEFT / RIGHT     SPRINT: SHIFT\nJUMP: SPACE, W, UP, OR GAMEPAD A\nDodge tree stumps, branches, thorns, and creatures.\nCollect sun coins and activate checkpoints.\nLightning gives a speed boost. Magnet pulls nearby coins.",new Vector2(.5f,.52f),new Vector2(860,190),20);SetMenuPage(MenuPage.HowToPlay);UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(sceneHowToPlayBackButton);}
    void ToggleSound(){if(audio)audio.SetSoundEnabled(!audio.SoundEnabled);UpdateSoundButton();}
    void UpdateSoundButton(){if(!sceneSoundButton)return;var label=sceneSoundButton.GetComponentInChildren<Text>();if(label)label.text=(audio==null||audio.SoundEnabled)?"SOUND  ON":"SOUND  OFF";}
    GameObject BuildCoin(Vector2 at){
        var coin=new GameObject("Sun coin");coin.transform.position=new Vector3(at.x,at.y,0);
        var pickup=coin.AddComponent<CircleCollider2D>();pickup.radius=.62f;pickup.isTrigger=true;
        var glow=new GameObject("Soft gold coin aura");glow.transform.SetParent(coin.transform,false);glow.transform.localPosition=new Vector3(0,0,.08f);
        var glowRenderer=glow.AddComponent<SpriteRenderer>();glowRenderer.sprite=Resources.Load<Sprite>("Artwork/JungleCoinGlow");glowRenderer.color=Color.white;glowRenderer.sortingOrder=9;
        var visual=new GameObject("Golden coin artwork");visual.transform.SetParent(coin.transform,false);visual.transform.localPosition=Vector3.zero;visual.transform.localScale=Vector3.one*.82f;
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=Resources.Load<Sprite>("Artwork/JungleCoinArt");renderer.sortingOrder=10;renderer.color=Color.white;
        coin.AddComponent<JungleCoin>().Configure(visual.transform,glow.transform,at.x*.23f);
        return coin;
    }
    void BuildBackdrop(){var artA=Resources.Load<Sprite>("JungleBackdrop");var artB=Resources.Load<Sprite>("JungleBackdropB");if(!artA){var sprites=Resources.LoadAll<Sprite>("JungleBackdrop");if(sprites.Length>0)artA=sprites[0];}if(!artB){var sprites=Resources.LoadAll<Sprite>("JungleBackdropB");if(sprites.Length>0)artB=sprites[0];}if(!artA){Debug.LogError("JungleBackdrop sprite was not imported. Check the Jungle Quest texture import settings.");return;}if(!artB)artB=artA;
        const float w=25f;for(int i=-1;i<9;i++){var art=(i%2==0)?artA:artB;float h=w/art.bounds.size.x*art.bounds.size.y;var g=new GameObject("Painted rainforest panorama");g.transform.position=new Vector3(i*w,0,20);g.transform.localScale=new Vector3(w/art.bounds.size.x,h/art.bounds.size.y,1);var sr=g.AddComponent<SpriteRenderer>();sr.sprite=art;sr.sortingOrder=-100;}
    }
    GameObject BuildExplorer(Vector2 at){
        var p=new GameObject("Explorer");p.transform.position=new Vector3(at.x,at.y,0);p.transform.localScale=Vector3.one;
        var sheet=Resources.Load<Texture2D>("ExplorerSpriteSheet");
        if(sheet&&sheet.width>=4&&sheet.height>=2){
            try{
                var frames=GetExplorerFrames(sheet);float fw=sheet.width/4,fh=sheet.height/2;
                var art=new GameObject("Animated hand-painted explorer");art.transform.SetParent(p.transform,false);art.transform.localPosition=new Vector3(0,-.58f,-.2f);
                art.transform.localScale=new Vector3(1.52f/(fw/512f),1.72f/(fh/512f),1);var renderer=art.AddComponent<SpriteRenderer>();renderer.sprite=frames[4];renderer.sortingOrder=50;renderer.enabled=true;
                p.AddComponent<JunglePlayerVisual>().SetSpriteAnimation(renderer,new[]{frames[0],frames[1],frames[2],frames[3]},frames[4],frames[5],frames[6],frames[7]);return p;
            }catch(System.Exception e){Debug.LogError("Explorer animation sheet could not be sliced; using the visible sprite fallback. "+e.Message);Destroy(p);p=new GameObject("Explorer");p.transform.position=new Vector3(at.x,at.y,0);p.transform.localScale=Vector3.one;}
        }
        var imported=Resources.Load<Sprite>("ExplorerSprite");if(!imported){var sprites=Resources.LoadAll<Sprite>("ExplorerSprite");if(sprites.Length>0)imported=sprites[0];}
        if(imported){var art=new GameObject("Painted explorer sprite");art.transform.SetParent(p.transform,false);art.transform.localPosition=new Vector3(0,.12f,-.2f);art.transform.localScale=new Vector3(1.6f/imported.bounds.size.x,1.75f/imported.bounds.size.y,1);var renderer=art.AddComponent<SpriteRenderer>();renderer.sprite=imported;renderer.sortingOrder=20;p.AddComponent<JunglePlayerVisual>().SetSpriteAnimation(renderer,new[]{imported,imported,imported,imported},imported,imported,imported,imported);return p;}
        Detail(p.transform,"Olive adventure backpack",new Vector2(-.28f,.02f),new Vector2(.48f,.69f),new Color(.36f,.31f,.13f),0,4);
        Detail(p.transform,"Backpack pocket",new Vector2(-.3f,-.08f),new Vector2(.26f,.25f),new Color(.67f,.48f,.19f),-1,4);
        Detail(p.transform,"Explorer torso",new Vector2(.03f,-.12f),new Vector2(.62f,.69f),new Color(.12f,.47f,.37f),-1,4);
        Detail(p.transform,"Shirt highlight",new Vector2(.12f,.02f),new Vector2(.22f,.46f),new Color(.24f,.67f,.49f),-2,4);
        Detail(p.transform,"Neck",new Vector2(.09f,.35f),new Vector2(.26f,.2f),new Color(.76f,.42f,.23f),-2,4);
        Detail(p.transform,"Explorer face",new Vector2(.1f,.57f),new Vector2(.53f,.48f),new Color(.91f,.61f,.38f),-2,0);
        Detail(p.transform,"Explorer hair",new Vector2(.06f,.75f),new Vector2(.57f,.23f),new Color(.22f,.12f,.08f),-3,4);
        Detail(p.transform,"Adventure hat brim",new Vector2(.08f,.86f),new Vector2(.84f,.16f),new Color(.91f,.69f,.27f),-4,4);
        Detail(p.transform,"Adventure hat crown",new Vector2(.08f,.98f),new Vector2(.49f,.3f),new Color(.78f,.52f,.17f),-4,4);
        Detail(p.transform,"Hat band",new Vector2(.08f,.92f),new Vector2(.5f,.075f),new Color(.39f,.23f,.11f),-5,4);
        Detail(p.transform,"Eye white",new Vector2(.27f,.59f),new Vector2(.095f,.14f),new Color(1f,.94f,.78f),-5,0);
        Detail(p.transform,"Eye pupil",new Vector2(.29f,.58f),new Vector2(.045f,.085f),new Color(.12f,.12f,.09f),-6,0);
        Detail(p.transform,"Smile",new Vector2(.25f,.43f),new Vector2(.14f,.055f),new Color(.4f,.16f,.11f),-5,2);
        var armL=Detail(p.transform,"Swinging arm",new Vector2(.35f,-.19f),new Vector2(.2f,.54f),new Color(.91f,.61f,.38f),-2,4,0);
        var legL=Detail(p.transform,"Near trouser leg",new Vector2(-.12f,-.67f),new Vector2(.24f,.42f),new Color(.43f,.32f,.19f),-2,4);
        Detail(p.transform,"Near boot",new Vector2(-.1f,-.89f),new Vector2(.35f,.16f),new Color(.25f,.16f,.11f),-3,4);
        Detail(p.transform,"Far trouser leg",new Vector2(.15f,-.67f),new Vector2(.24f,.42f),new Color(.55f,.39f,.22f),-3,4);
        Detail(p.transform,"Far boot",new Vector2(.18f,-.89f),new Vector2(.35f,.16f),new Color(.34f,.2f,.12f),-4,4);
        var visualRoot=new GameObject("Procedural explorer visual");visualRoot.transform.SetParent(p.transform,false);visualRoot.transform.localPosition=new Vector3(0,.33f,0);while(p.transform.childCount>1)p.transform.GetChild(0).SetParent(visualRoot.transform,false);
        var visual=p.AddComponent<JunglePlayerVisual>();visual.SetAnimatedParts(legL.transform,armL.transform);return p;
    }
    static Sprite[] GetExplorerFrames(Texture2D sheet){
        if(cachedExplorerSheet==sheet&&cachedExplorerFrames!=null)return cachedExplorerFrames;
        int fw=sheet.width/4,fh=sheet.height/2,topY=sheet.height-fh;var frames=new Sprite[8];
        for(int i=0;i<4;i++)frames[i]=Sprite.Create(sheet,new Rect(i*fw,topY,fw,fh),new Vector2(.5f,.035f),512,0,SpriteMeshType.FullRect);
        for(int i=0;i<4;i++)frames[i+4]=Sprite.Create(sheet,new Rect(i*fw,0,fw,fh),new Vector2(.5f,.035f),512,0,SpriteMeshType.FullRect);
        cachedExplorerSheet=sheet;cachedExplorerFrames=frames;return frames;
    }
    GameObject BuildBeetle(Vector2 at){
        var b=new GameObject("Patrolling jungle beetle");b.transform.position=at;
        var art=new GameObject("Beetle animated body");art.transform.SetParent(b.transform,false);
        Detail(art.transform,"Soft ground shadow",new Vector2(0,-.38f),new Vector2(1.28f,.2f),new Color(.04f,.08f,.04f,.38f),3,2);
        Detail(art.transform,"Dark beetle abdomen",new Vector2(-.02f,-.12f),new Vector2(.97f,.44f),new Color(.16f,.055f,.045f),1,0);
        Detail(art.transform,"Shell lower rim",new Vector2(-.08f,.06f),new Vector2(1.12f,.67f),new Color(.32f,.065f,.045f),1,0);
        Detail(art.transform,"Left lacquered wing case",new Vector2(-.31f,.15f),new Vector2(.53f,.56f),new Color(.7f,.12f,.075f),0,0);
        Detail(art.transform,"Right lacquered wing case",new Vector2(.17f,.15f),new Vector2(.51f,.56f),new Color(.82f,.19f,.09f),-1,0);
        Detail(art.transform,"Wing case highlight",new Vector2(-.28f,.34f),new Vector2(.34f,.09f),new Color(1f,.43f,.2f),-2,2,-16);
        Detail(art.transform,"Wing case seam",new Vector2(-.07f,.16f),new Vector2(.025f,.47f),new Color(.34f,.055f,.035f),-2,4);
        for(int i=0;i<4;i++){float x=-.44f+(i%2)*.23f;float y=.04f+(i/2)*.2f;Detail(art.transform,"Shell pore",new Vector2(x,y),new Vector2(.055f,.045f),new Color(.45f,.075f,.045f),-2,0);}
        Detail(art.transform,"Amber thorax plate",new Vector2(.36f,.05f),new Vector2(.34f,.4f),new Color(.59f,.13f,.065f),-1,0);
        Detail(art.transform,"Dark beetle head",new Vector2(.57f,-.045f),new Vector2(.28f,.25f),new Color(.2f,.075f,.05f),-2,0);
        Detail(art.transform,"Beetle eye shine",new Vector2(.65f,.025f),new Vector2(.075f,.1f),new Color(.98f,.77f,.35f),-3,0);
        Detail(art.transform,"Beetle pupil",new Vector2(.672f,.025f),new Vector2(.034f,.065f),new Color(.075f,.035f,.025f),-4,0);
        var antennaA=Detail(art.transform,"Near antenna",new Vector2(.59f,.17f),new Vector2(.045f,.22f),new Color(.12f,.055f,.04f),-2,4,-24);
        var antennaB=Detail(art.transform,"Far antenna",new Vector2(.7f,.16f),new Vector2(.04f,.2f),new Color(.12f,.055f,.04f),-1,4,18);
        Transform[] legs=new Transform[6];int legIndex=0;
        for(int i=0;i<3;i++)for(int side=0;side<2;side++){
            float x=-.3f+i*.3f;float z=side==0?2:-2;var joint=new GameObject("Beetle leg joint");joint.transform.SetParent(art.transform,false);joint.transform.localPosition=new Vector3(x,-.14f+side*.025f,z);
            var limb=Detail(joint.transform,side==0?"Far walking leg":"Near walking leg",new Vector2((i-1)*.045f,-.13f),new Vector2(.31f,.075f),side==0?new Color(.18f,.075f,.055f):new Color(.28f,.105f,.065f),0,4,(i-1)*19+(side==0?12:-12));
            Detail(joint.transform,"Leg joint",new Vector2((i-1)*.035f,.015f),new Vector2(.085f,.085f),new Color(.35f,.13f,.075f),-1,0);
            legs[legIndex++]=joint.transform;
        }
        b.AddComponent<JungleBeetleVisual>().Configure(art.transform,legs,antennaA.transform,antennaB.transform);
        return b;
    }
    GameObject BuildOwl(Vector2 at){
        var owl=new GameObject("Diving jungle owl");owl.transform.position=at;
        var art=new GameObject("Owl flight animation pivot");art.transform.SetParent(owl.transform,false);
        Detail(art.transform,"Owl tail feathers",new Vector2(-.4f,-.22f),new Vector2(.25f,.42f),new Color(.29f,.17f,.095f),2,3,-72);
        Detail(art.transform,"Owl body silhouette",new Vector2(-.02f,-.1f),new Vector2(.75f,.9f),new Color(.34f,.2f,.11f),1,0);
        Detail(art.transform,"Warm feather mantle",new Vector2(-.06f,-.15f),new Vector2(.62f,.72f),new Color(.53f,.34f,.19f),0,0);
        Detail(art.transform,"Cream chest plumage",new Vector2(.04f,-.25f),new Vector2(.48f,.55f),new Color(.9f,.79f,.59f),-1,0);
        Detail(art.transform,"Owl head",new Vector2(.03f,.32f),new Vector2(.72f,.67f),new Color(.42f,.26f,.14f),-1,0);
        Detail(art.transform,"Left ear tuft",new Vector2(-.24f,.69f),new Vector2(.2f,.34f),new Color(.37f,.22f,.12f),-2,3,-18);
        Detail(art.transform,"Right ear tuft",new Vector2(.28f,.69f),new Vector2(.2f,.34f),new Color(.37f,.22f,.12f),-2,3,18);
        Detail(art.transform,"Left facial disc",new Vector2(-.15f,.33f),new Vector2(.34f,.39f),new Color(.89f,.78f,.57f),-2,0);
        Detail(art.transform,"Right facial disc",new Vector2(.21f,.33f),new Vector2(.34f,.39f),new Color(.89f,.78f,.57f),-2,0);
        Detail(art.transform,"Left eye socket",new Vector2(-.14f,.34f),new Vector2(.22f,.27f),new Color(.21f,.12f,.075f),-3,0);
        Detail(art.transform,"Right eye socket",new Vector2(.2f,.34f),new Vector2(.22f,.27f),new Color(.21f,.12f,.075f),-3,0);
        Detail(art.transform,"Left amber iris",new Vector2(-.14f,.34f),new Vector2(.155f,.19f),new Color(.92f,.58f,.12f),-4,0);
        Detail(art.transform,"Right amber iris",new Vector2(.2f,.34f),new Vector2(.155f,.19f),new Color(.92f,.58f,.12f),-4,0);
        Detail(art.transform,"Left pupil",new Vector2(-.125f,.34f),new Vector2(.085f,.13f),new Color(.06f,.045f,.03f),-5,0);
        Detail(art.transform,"Right pupil",new Vector2(.215f,.34f),new Vector2(.085f,.13f),new Color(.06f,.045f,.03f),-5,0);
        Detail(art.transform,"Eye catchlight",new Vector2(-.15f,.39f),new Vector2(.04f,.05f),new Color(1f,.98f,.84f),-6,0);
        Detail(art.transform,"Eye catchlight",new Vector2(.19f,.39f),new Vector2(.04f,.05f),new Color(1f,.98f,.84f),-6,0);
        Detail(art.transform,"Hooked beak",new Vector2(.03f,.13f),new Vector2(.16f,.2f),new Color(.92f,.48f,.075f),-6,6,180);
        var farWing=new GameObject("Far feathered wing pivot");farWing.transform.SetParent(art.transform,false);farWing.transform.localPosition=new Vector3(.33f,.02f,.02f);farWing.transform.localScale=new Vector3(-1,1,1);
        Detail(farWing.transform,"Far wing",new Vector2(-.22f,.02f),new Vector2(.68f,.3f),new Color(.38f,.23f,.13f),0,3,-8);
        for(int i=0;i<3;i++)Detail(farWing.transform,"Far wing feather",new Vector2(-.36f+i*.14f,-.03f),new Vector2(.29f,.11f),new Color(.68f,.5f,.3f),-1,3,-15-i*4);
        var nearWing=new GameObject("Near feathered wing pivot");nearWing.transform.SetParent(art.transform,false);nearWing.transform.localPosition=new Vector3(-.33f,.03f,-.03f);
        Detail(nearWing.transform,"Near wing",new Vector2(-.22f,.02f),new Vector2(.68f,.3f),new Color(.48f,.29f,.16f),0,3,8);
        for(int i=0;i<3;i++)Detail(nearWing.transform,"Near wing feather",new Vector2(-.36f+i*.14f,-.03f),new Vector2(.29f,.11f),new Color(.76f,.58f,.35f),-1,3,15+i*4);
        Detail(art.transform,"Left talon",new Vector2(-.17f,-.55f),new Vector2(.2f,.085f),new Color(.8f,.39f,.065f),-2,4,-12);
        Detail(art.transform,"Right talon",new Vector2(.16f,-.55f),new Vector2(.2f,.085f),new Color(.8f,.39f,.065f),-2,4,12);
        for(int i=-1;i<=1;i+=2)Detail(art.transform,"Curved talon tip",new Vector2(i*.23f,-.61f),new Vector2(.11f,.07f),new Color(.62f,.27f,.045f),-3,3,i*25);
        owl.AddComponent<JungleOwlVisual>().Configure(art.transform,farWing.transform,nearWing.transform);
        return owl;
    }
    void AddPatrollingBeetle(Vector2 at,float left,float right){
        var beetle=BuildBeetle(at);var collider=beetle.AddComponent<BoxCollider2D>();collider.size=new Vector2(1.42f,.88f);collider.offset=new Vector2(0,-.02f);collider.isTrigger=true;
        var body=beetle.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;body.gravityScale=0;body.sleepMode=RigidbodySleepMode2D.NeverSleep;body.interpolation=RigidbodyInterpolation2D.Interpolate;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;body.WakeUp();
        beetle.AddComponent<JungleEnemy>();var patrol=beetle.AddComponent<JunglePatrol>();patrol.left=left;patrol.right=right;
    }
    void AddPatrollingOwl(Vector2 at,float left,float right){
        var owl=BuildOwl(at);var collider=owl.AddComponent<BoxCollider2D>();collider.size=new Vector2(1.8f,1.65f);collider.offset=new Vector2(0,.03f);collider.isTrigger=true;
        var body=owl.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;body.gravityScale=0;body.sleepMode=RigidbodySleepMode2D.NeverSleep;body.interpolation=RigidbodyInterpolation2D.Interpolate;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;body.WakeUp();
        owl.AddComponent<JungleEnemy>();var patrol=owl.AddComponent<JungleOwlPatrol>();patrol.left=left;patrol.right=right;
    }
    void BuildCheckpoint(float x){
        Shape("Checkpoint banner",new Vector2(x,-1.9f),new Vector2(.3f,4),new Color(.74f,.32f,.12f),0);
        var flag=Shape("Checkpoint flag",new Vector2(x+.7f,-.3f),new Vector2(1.6f,.9f),new Color(1f,.78f,.24f),0,1);
        var trigger=flag.AddComponent<BoxCollider2D>();trigger.size=new Vector2(1.8f,4f);trigger.offset=new Vector2(-.7f,-1.2f);trigger.isTrigger=true;flag.AddComponent<JungleCheckpoint>();
    }
    GameObject BuildTempleExit(Vector2 at){
        var root=new GameObject("Ancient temple exit");root.transform.position=at;
        Detail(root.transform,"Temple shadow",new Vector2(0,-.16f),new Vector2(3.5f,4.8f),new Color(.46f,.34f,.2f),0,4);
        Detail(root.transform,"Temple doorway",new Vector2(0,-.58f),new Vector2(1.25f,2.5f),new Color(.12f,.23f,.2f),-1,4);
        Detail(root.transform,"Temple lintel",new Vector2(0,1.98f),new Vector2(3.7f,.48f),new Color(.84f,.68f,.35f),-1,4);
        for(int x=-1;x<=1;x+=2){Detail(root.transform,"Carved temple pillar",new Vector2(x*1.28f,.65f),new Vector2(.42f,2.7f),new Color(.71f,.56f,.29f),-1,4);for(int y=0;y<3;y++)Detail(root.transform,"Pillar carving",new Vector2(x*1.28f,-.25f+y*.75f),new Vector2(.47f,.09f),new Color(.93f,.78f,.48f),-2,4);}
        Detail(root.transform,"Temple sun emblem",new Vector2(0,1.18f),new Vector2(.68f,.68f),new Color(1f,.79f,.25f),-2,1);
        for(int i=0;i<4;i++){var vine=Detail(root.transform,"Temple creeper",new Vector2(-1.48f+i*.22f,1.8f-i*.68f),new Vector2(.14f,1.85f),new Color(.16f,.48f,.25f),-3,4,i*8);Detail(root.transform,"Creeper leaf",new Vector2(-1.42f+i*.22f,1.1f-i*.62f),new Vector2(.42f,.2f),new Color(.31f,.62f,.25f),-4,3,-30);}
        return root;
    }
    void BuildFern(Vector2 at,Color color,int z,float scale){
        var stem=Shape("Fern stem",at+new Vector2(0,.22f*scale),new Vector2(.12f,1.1f*scale),new Color(.4f,.68f,.27f),z,4);stem.AddComponent<JungleSway>().Configure(4f,.9f);
        for(int i=0;i<6;i++){float y=-.18f*scale+i*.14f*scale;for(int side=-1;side<=1;side+=2){var l=Shape("Lush fern leaflet",at+new Vector2(side*(.18f+i*.09f)*scale,y),new Vector2((.58f-i*.035f)*scale,.25f*scale),color,z-1,3);l.transform.rotation=Quaternion.Euler(0,0,side*(20+i*4));l.AddComponent<JungleSway>().Configure(5f+i*.22f,side*3.2f);}}
    }
    void BuildAtmosphere(){
        // Broken sunbeams, drifting motes and seed fluff create motion across the long painted backdrop.
        for(int i=0;i<58;i++){
            float x=-8f+(i*3.11f)%190f;float y=-.6f+((i*1.73f)%6.2f);var g=Shape("Floating jungle pollen",new Vector2(x,y),new Vector2(.075f,.075f),i%4==0?new Color(1f,.87f,.45f,.7f):new Color(.8f,.94f,.58f,.55f),-3,0);
            var halo=Shape("Pollen glow",new Vector2(x,y),new Vector2(.22f,.22f),new Color(.8f,1f,.56f,.11f),-4,0);g.AddComponent<JungleFirefly>().Configure(halo.transform,i*.73f,.4f+(i%5)*.12f);
        }
        for(int i=0;i<16;i++){
            float x=-4f+i*12.1f;var branch=Shape("Hanging jungle vine",new Vector2(x,4.5f),new Vector2(.09f,2.1f+(i%3)*.35f),new Color(.15f,.37f,.18f),-1,4);branch.AddComponent<JungleSway>().Configure(2.1f,2.4f);
            for(int j=0;j<4;j++){var leaflet=Shape("Vine leaf",new Vector2(x+(j%2==0?.22f:-.2f),3.95f-j*.42f),new Vector2(.48f,.19f),j%2==0?new Color(.27f,.6f,.23f):new Color(.16f,.49f,.24f),-2,3);leaflet.transform.rotation=Quaternion.Euler(0,0,j%2==0?-28:28);leaflet.AddComponent<JungleSway>().Configure(2.7f,4f);}
        }
        for(int i=0;i<10;i++){var bird=Shape("Distant jungle bird",new Vector2(-10-i*19f,3.4f+(i%3)*.58f),new Vector2(.5f,.12f),new Color(.08f,.24f,.23f,.76f),15,5);bird.AddComponent<JungleBird>().Configure(i*2.9f, .7f+(i%4)*.22f);}
    }
    void MakeFallenLog(float x,float y,float w){
        var h=new GameObject("Mossy tree stump • dangerous obstacle");h.transform.position=new Vector3(x,y,0);
        var stumpArt=Resources.Load<Sprite>("Artwork/TreeTrunkArt")??MakeFallenLogArtwork();
        float width=Mathf.Clamp(w*.55f,1.7f,1.9f),height=width*stumpArt.bounds.size.y/Mathf.Max(.001f,stumpArt.bounds.size.x);
        var collider=h.AddComponent<BoxCollider2D>();collider.size=new Vector2(width*.78f,.84f);collider.offset=new Vector2(0,.42f);collider.isTrigger=true;h.AddComponent<JungleHazard>();
        AddSpriteArtwork(h.transform,"Tree stump obstacle artwork",stumpArt,new Vector2(0,height*.5f),new Vector2(width,height),1,-.01f);
    }
    void MakeTreeStump(float x,float y,float scale){
        var root=new GameObject("Mossy tree stump • dangerous obstacle");root.transform.position=new Vector3(x,y,0);
        var collider=root.AddComponent<BoxCollider2D>();collider.size=new Vector2(1.1f*scale,.95f*scale);collider.offset=new Vector2(0,.43f*scale);collider.isTrigger=true;root.AddComponent<JungleHazard>();
        var stumpArt=Resources.Load<Sprite>("Artwork/TreeTrunkArt")??MakeFallenLogArtwork();
        AddSpriteArtwork(root.transform,"Flared cut tree stump artwork",stumpArt,new Vector2(0,.46f*scale),new Vector2(1.42f*scale,.94f*scale),2,-.01f);
    }
    void MakeThornGrassHazard(float x,float y,float scale){
        var root=new GameObject("Grass hidden spikes • dangerous obstacle");root.transform.position=new Vector3(x,y,0);
        var collider=root.AddComponent<BoxCollider2D>();collider.size=new Vector2(1.55f*scale,.7f*scale);collider.offset=new Vector2(0,.2f*scale);collider.isTrigger=true;root.AddComponent<JungleHazard>();
        var thorns=Resources.Load<Sprite>("Artwork/ThornCrownArt");
        AddSpriteArtwork(root.transform,"Thorn crown hazard artwork",thorns,new Vector2(0,.2f*scale),new Vector2(1.55f*scale,.7f*scale),2,-.01f);
    }
    void BuildEnvironmentDetails(){
        float[] rockXs={2.5f,18.5f,39f,62f,80f,101f,119f,139f,158f,177f};
        for(int i=0;i<rockXs.Length;i++){float x=rockXs[i];var rock=Shape("Jungle river rock",new Vector2(x,-3.72f),new Vector2(.8f+(i%3)*.16f,.47f),i%2==0?new Color(.26f,.3f,.22f):new Color(.36f,.34f,.25f),-2,2);rock.transform.rotation=Quaternion.Euler(0,0,(i%2==0?-1:1)*(8+i%4*5));}
        float[] mushroomXs={7f,37f,73f,102f,143f,161f};
        for(int i=0;i<mushroomXs.Length;i++){float x=mushroomXs[i],y=-3.58f;Shape("Jungle mushroom stem",new Vector2(x,y+.16f),new Vector2(.16f,.36f),new Color(.88f,.79f,.53f),-1,2);Shape("Jungle mushroom cap",new Vector2(x,y+.36f),new Vector2(.48f,.27f),i%2==0?new Color(.75f,.2f,.14f):new Color(.61f,.32f,.75f),-2,2);for(int dot=0;dot<3;dot++)Shape("Mushroom cap spot",new Vector2(x+(dot-1)*.12f,y+.39f),new Vector2(.045f,.045f),new Color(1f,.86f,.58f),-3,0);}
        float[] flowerXs={14f,41f,69f,106f,132f,163f};for(int i=0;i<flowerXs.Length;i++){float x=flowerXs[i],y=-3.64f;for(int petal=0;petal<5;petal++){float a=petal*Mathf.PI*2/5f;Shape("Jungle flower petal",new Vector2(x+Mathf.Cos(a)*.11f,y+.24f+Mathf.Sin(a)*.11f),new Vector2(.14f,.12f),i%2==0?new Color(1f,.55f,.68f):new Color(.94f,.78f,.25f),-2,0);}Shape("Jungle flower heart",new Vector2(x,y+.24f),new Vector2(.1f,.1f),new Color(.35f,.16f,.06f),-3,0);}
    }
    GameObject AddSpriteArtwork(Transform parent,string name,Sprite sprite,Vector2 localPosition,Vector2 worldSize,int sortingOrder,float localZ=0){
        if(!sprite){Debug.LogError($"The transparent {name} sprite is missing from Resources.");return null;}
        var art=new GameObject(name);art.transform.SetParent(parent,false);art.transform.localPosition=new Vector3(localPosition.x,localPosition.y,localZ);
        art.transform.localScale=new Vector3(worldSize.x/Mathf.Max(.001f,sprite.bounds.size.x),worldSize.y/Mathf.Max(.001f,sprite.bounds.size.y),1);
        var renderer=art.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=Color.white;renderer.sortingOrder=sortingOrder;return art;
    }
    void BuildPowerup(Vector2 at,JunglePowerupKind kind){
        Color color=kind==JunglePowerupKind.Speed?new Color(1f,.68f,.12f):new Color(.2f,.72f,.98f);
        Sprite artwork=Resources.Load<Sprite>(kind==JunglePowerupKind.Speed?"Artwork/PowerupLightning":"Artwork/PowerupMagnet");
        var pickup=new GameObject("Powerup • "+kind);pickup.transform.position=at;
        var trigger=pickup.AddComponent<CircleCollider2D>();trigger.radius=.5f;trigger.isTrigger=true;
        pickup.AddComponent<JunglePowerup>().Configure(kind);
        var glow=Shape("Powerup glow",at,new Vector2(1.12f,1.12f),new Color(color.r,color.g,color.b,.14f),-1,0);
        glow.transform.SetParent(pickup.transform,true);
        if(!artwork){Debug.LogError("Powerup artwork is missing from Resources/Artwork.");return;}
        var icon=new GameObject(kind==JunglePowerupKind.Speed?"Lightning powerup artwork":"Magnet powerup artwork");
        icon.transform.SetParent(pickup.transform,false);
        icon.transform.localPosition=new Vector3(0,0,-.02f);
        float iconScale=.94f/Mathf.Max(.001f,Mathf.Max(artwork.bounds.size.x,artwork.bounds.size.y));
        icon.transform.localScale=Vector3.one*iconScale;
        var renderer=icon.AddComponent<SpriteRenderer>();renderer.sprite=artwork;renderer.color=Color.white;renderer.sortingOrder=1;
    }
    void BuildUI(){
        var es=new GameObject("EventSystem",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
        var canvasGO=new GameObject("Jungle Quest UI",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=canvasGO.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvasGO.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var hudRim=UIImage(canvas.transform,"HUD gold rim",Vector2.zero,Vector2.zero,new Color(.47f,.34f,.14f,.96f),4);var hudRimRect=(RectTransform)hudRim.transform;hudRimRect.anchorMin=new Vector2(0,1);hudRimRect.anchorMax=new Vector2(.60f,1);hudRimRect.pivot=new Vector2(0,1);hudRimRect.offsetMin=new Vector2(16,-96);hudRimRect.offsetMax=new Vector2(-18,-18);
        var hudPlate=UIImage(canvas.transform,"HUD forest panel",Vector2.zero,Vector2.zero,new Color(.025f,.12f,.105f,.97f),4);var hudPlateRect=(RectTransform)hudPlate.transform;hudPlateRect.anchorMin=new Vector2(0,1);hudPlateRect.anchorMax=new Vector2(.60f,1);hudPlateRect.pivot=new Vector2(0,1);hudPlateRect.offsetMin=new Vector2(20,-92);hudPlateRect.offsetMax=new Vector2(-22,-22);
        hud=Label(canvas.transform,"SCORE  0  |  COINS  0 / 39  |  LIVES  3  |  CHECKPOINT  START  |  POWER  NONE",21,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Color(.96f,.98f,.92f));hud.rectTransform.anchorMin=new Vector2(0,1);hud.rectTransform.anchorMax=new Vector2(.60f,1);hud.rectTransform.pivot=new Vector2(0,1);hud.rectTransform.offsetMin=new Vector2(38,-86);hud.rectTransform.offsetMax=new Vector2(-36,-28);hud.alignment=TextAnchor.MiddleLeft;hud.fontStyle=FontStyle.Bold;hud.resizeTextForBestFit=true;hud.resizeTextMinSize=15;hud.resizeTextMaxSize=21;hud.horizontalOverflow=HorizontalWrapMode.Wrap;hud.verticalOverflow=VerticalWrapMode.Truncate;AddTextShadow(hud,new Color(.005f,.025f,.018f,.9f),new Vector2(1,-1));
        overlay=Panel(canvas.transform,"Overlay",new Color(.015f,.045f,.04f,.5f));
        var menu=new GameObject("Menu content",typeof(RectTransform));menu.transform.SetParent(overlay.transform,false);var menuRect=(RectTransform)menu.transform;menuRect.anchorMin=Vector2.zero;menuRect.anchorMax=Vector2.one;menuRect.offsetMin=menuRect.offsetMax=Vector2.zero;
        UIImage(menu.transform,"Plaque shadow",new Vector2(964,764),new Vector2(0,-12),new Color(.005f,.025f,.018f,.8f),4);
        UIImage(menu.transform,"Jungle menu plaque",new Vector2(948,748),Vector2.zero,new Color(.48f,.34f,.13f,1f),4);
        UIImage(menu.transform,"Plaque inner edge",new Vector2(930,730),Vector2.zero,new Color(.21f,.39f,.22f,1f),4);
        UIImage(menu.transform,"Plaque inset",new Vector2(912,712),Vector2.zero,new Color(.025f,.14f,.115f,.99f),4);
        var kicker=Label(menu.transform,"A WILD RUNNER ADVENTURE",17,new Vector2(.5f,.82f),new Vector2(.5f,.5f),Vector2.zero,new Color(.69f,.85f,.67f));kicker.rectTransform.sizeDelta=new Vector2(760,34);kicker.fontStyle=FontStyle.Bold;
        titleText=Label(menu.transform,"JUNGLE ADVENTURE",62,new Vector2(.5f,.73f),new Vector2(.5f,.5f),Vector2.zero,new Color(1f,.79f,.3f));titleText.rectTransform.sizeDelta=new Vector2(850,84);AddTextShadow(titleText,new Color(.04f,.09f,.04f,.9f),new Vector2(3,-4));
        UIImage(menu.transform,"Title gold divider",new Vector2(270,4),new Vector2(0,185),new Color(1f,.72f,.2f,.94f),4);
        messageText=Label(menu.transform,"A lost expedition. Choose your explorer,\nthen set off into the wild.",22,new Vector2(.5f,.615f),new Vector2(.5f,.5f),Vector2.zero,new Color(.91f,.95f,.86f));SetMenuMessage("A lost expedition. Choose your explorer,\nthen set off into the wild.",new Vector2(.5f,.615f),new Vector2(860,82),22);
        sceneMainMenuPage=Page(menu.transform,"Main menu page");
        Button(sceneMainMenuPage.transform,"PLAY",new Vector2(0,-105),StartGame,new Vector2(330,66),26);
        sceneCharacterOpenButton=Button(sceneMainMenuPage.transform,"CHARACTER SELECTION",new Vector2(0,-177),ShowCharacterSelection,new Vector2(420,66),23);
        sceneHowToPlayButton=Button(sceneMainMenuPage.transform,"HOW TO PLAY",new Vector2(0,-249),ShowHowToPlay,new Vector2(330,66),23);
        sceneQuitButton=Button(sceneMainMenuPage.transform,"EXIT GAME",new Vector2(0,-321),ExitGame,new Vector2(330,66),23);
        sceneCharacterPicker=new GameObject("Character selection",typeof(RectTransform));sceneCharacterPicker.transform.SetParent(menu.transform,false);var pickerRect=(RectTransform)sceneCharacterPicker.transform;pickerRect.anchorMin=Vector2.zero;pickerRect.anchorMax=Vector2.one;pickerRect.offsetMin=pickerRect.offsetMax=Vector2.zero;
        var selectionLabel=Label(sceneCharacterPicker.transform,"SELECT YOUR ADVENTURER",22,new Vector2(.5f,.55f),new Vector2(.5f,.5f),Vector2.zero,new Color(.76f,.9f,.7f));selectionLabel.rectTransform.sizeDelta=new Vector2(850,44);selectionLabel.fontStyle=FontStyle.Bold;
        sceneCharacterButtons=new GameObject[4];for(int i=0;i<sceneCharacterButtons.Length;i++)sceneCharacterButtons[i]=CharacterCard(sceneCharacterPicker.transform,i,new Vector2((i-1.5f)*220,-60));
        sceneCharacterPreviousButton=Button(sceneCharacterPicker.transform,"PREVIOUS",new Vector2(-235,-163),()=>CycleCharacter(-1),new Vector2(230,60),20);
        sceneCharacterNextButton=Button(sceneCharacterPicker.transform,"NEXT",new Vector2(235,-163),()=>CycleCharacter(1),new Vector2(230,60),20);
        sceneCharacterSelectButton=Button(sceneCharacterPicker.transform,"SELECT",new Vector2(-125,-252),CommitCharacterSelection,new Vector2(240,62),22);
        sceneCharacterBackButton=Button(sceneCharacterPicker.transform,"BACK",new Vector2(125,-252),ShowStart,new Vector2(240,62),22);
        sceneHowToPlayPage=Page(menu.transform,"How to play page");
        var howBack=Button(sceneHowToPlayPage.transform,"BACK",new Vector2(0,-278),ShowStart,new Vector2(300,64),23);sceneHowToPlayBackButton=howBack;
        scenePausePage=Page(menu.transform,"Pause page");resumeButton=Button(scenePausePage.transform,"RESUME",new Vector2(0,-110),Resume,new Vector2(330,66),25);
        restartButton=Button(scenePausePage.transform,"RESTART",new Vector2(0,-188),Restart,new Vector2(330,66),23);
        scenePauseRestartButton=restartButton;scenePauseMainMenuButton=Button(scenePausePage.transform,"MAIN MENU",new Vector2(0,-266),ReturnToMainMenu,new Vector2(330,66),23);
        sceneEndPage=Page(menu.transform,"Game over page");sceneRestartButton=Button(sceneEndPage.transform,"RESTART",new Vector2(0,-130),Restart,new Vector2(330,66),25);
        sceneEndMainMenuButton=Button(sceneEndPage.transform,"MAIN MENU",new Vector2(0,-218),ReturnToMainMenu,new Vector2(330,66),23);
        sceneOverlay=overlay;scenePlayButton=GetPageButton(sceneMainMenuPage.transform,"PLAY Button");sceneResumeButton=resumeButton;sceneHud=hud;sceneTitleText=titleText;sceneMessageText=messageText;
        scenePauseButton=Button(canvas.transform,"PAUSE",Vector2.zero,PauseGame,new Vector2(154,56),19);var pauseRect=(RectTransform)scenePauseButton.transform;pauseRect.anchorMin=pauseRect.anchorMax=new Vector2(1,1);pauseRect.anchoredPosition=new Vector2(-286,-54);
        sceneSoundButton=Button(canvas.transform,"SOUND ON",Vector2.zero,ToggleSound,new Vector2(166,56),17);var soundRect=(RectTransform)sceneSoundButton.transform;soundRect.anchorMin=soundRect.anchorMax=new Vector2(1,1);soundRect.anchoredPosition=new Vector2(-110,-54);
        UpdateCharacterCards();UpdateSoundButton();SetMenuPage(MenuPage.Main);UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(scenePlayButton);
    }
    GameObject Page(Transform parent,string name){var page=new GameObject(name,typeof(RectTransform));page.transform.SetParent(parent,false);var rect=(RectTransform)page.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;return page;}
    GameObject GetPageButton(Transform parent,string name){var t=parent.Find(name);return t?t.gameObject:null;}
    GameObject CharacterCard(Transform parent,int index,Vector2 position){
        var card=new GameObject(CharacterNames[index]+" selection",typeof(RectTransform),typeof(Image),typeof(Button),typeof(Outline));card.transform.SetParent(parent,false);var rect=(RectTransform)card.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(210,148);rect.anchoredPosition=position;
        var image=card.GetComponent<Image>();image.sprite=MakeSprite(Color.white,4);image.color=new Color(.07f,.22f,.19f,.97f);
        var outline=card.GetComponent<Outline>();outline.effectColor=new Color(.56f,.7f,.55f,.48f);outline.effectDistance=new Vector2(1,-1);
        var button=card.GetComponent<Button>();button.transition=Selectable.Transition.ColorTint;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,1f,.78f);colors.pressedColor=new Color(.75f,.87f,.72f);button.colors=colors;
        var portrait=new GameObject("Character portrait",typeof(RectTransform),typeof(Image));portrait.transform.SetParent(card.transform,false);var portraitRect=(RectTransform)portrait.transform;portraitRect.anchorMin=new Vector2(0,.5f);portraitRect.anchorMax=new Vector2(0,.5f);portraitRect.pivot=new Vector2(0,.5f);portraitRect.sizeDelta=new Vector2(66,118);portraitRect.anchoredPosition=new Vector2(8,0);var portraitImage=portrait.GetComponent<Image>();var frames=GetCharacterFrames(index);if(frames!=null)portraitImage.sprite=frames[4];portraitImage.preserveAspect=true;portraitImage.raycastTarget=false;
        var label=Label(card.transform,CharacterNames[index],14,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(42,0),new Color(.97f,.97f,.88f));label.rectTransform.sizeDelta=new Vector2(128,112);label.fontStyle=FontStyle.Bold;AddTextShadow(label,new Color(.01f,.035f,.025f,.85f),new Vector2(1,-1));return card;
    }
    void BuildCoinSparklePool(){
        sceneCoinSparkles=new GameObject[9];
        for(int i=0;i<sceneCoinSparkles.Length;i++){var sparkle=Shape("Coin sparkle "+(i+1),Vector2.zero,new Vector2(.12f,.2f),i%3==0?Color.white:new Color(1f,.83f,.25f),-2,1);sparkle.SetActive(false);sceneCoinSparkles[i]=sparkle;}
    }
#if UNITY_EDITOR
    public void PersistProceduralSpritesForEditor(){
        const string folder="Assets/JungleQuest/Generated";
        if(!UnityEditor.AssetDatabase.IsValidFolder(folder))UnityEditor.AssetDatabase.CreateFolder("Assets/JungleQuest","Generated");
        var replacements=new Dictionary<Sprite,Sprite>();
        var entries=new List<SpriteCacheKey>(spriteCache.Keys);
        for(int i=0;i<entries.Count;i++){var key=entries[i];var original=spriteCache[key];var persisted=PersistSpriteForEditor(original,folder+"/JungleShape_"+i.ToString("D3")+".png",128);if(persisted){replacements[original]=persisted;spriteCache[key]=persisted;}}
        foreach(var renderer in Resources.FindObjectsOfTypeAll<SpriteRenderer>())if(renderer&&renderer.gameObject.scene==gameObject.scene&&renderer.sprite&&replacements.TryGetValue(renderer.sprite,out var replacement)){renderer.sprite=replacement;UnityEditor.EditorUtility.SetDirty(renderer);}
        foreach(var image in Resources.FindObjectsOfTypeAll<Image>())if(image&&image.gameObject.scene==gameObject.scene&&image.sprite&&replacements.TryGetValue(image.sprite,out var replacement)){image.sprite=replacement;UnityEditor.EditorUtility.SetDirty(image);}
        UnityEditor.AssetDatabase.SaveAssets();
    }
    static Sprite PersistSpriteForEditor(Sprite source,string assetPath,float pixelsPerUnit){
        if(!source||!source.texture)return null;
        string projectRoot=System.IO.Directory.GetParent(Application.dataPath).FullName;
        string fullPath=System.IO.Path.Combine(projectRoot,assetPath.Replace('/',System.IO.Path.DirectorySeparatorChar));
        System.IO.File.WriteAllBytes(fullPath,source.texture.EncodeToPNG());
        UnityEditor.AssetDatabase.ImportAsset(assetPath,UnityEditor.ImportAssetOptions.ForceSynchronousImport);
        if(UnityEditor.AssetImporter.GetAtPath(assetPath) is TextureImporter importer){
            bool changed=false;
            if(importer.textureType!=TextureImporterType.Sprite){importer.textureType=TextureImporterType.Sprite;changed=true;}
            if(importer.spriteImportMode!=SpriteImportMode.Single){importer.spriteImportMode=SpriteImportMode.Single;changed=true;}
            if(!importer.alphaIsTransparency){importer.alphaIsTransparency=true;changed=true;}
            if(importer.mipmapEnabled){importer.mipmapEnabled=false;changed=true;}
            if(importer.filterMode!=FilterMode.Bilinear){importer.filterMode=FilterMode.Bilinear;changed=true;}
            if(!Mathf.Approximately(importer.spritePixelsPerUnit,pixelsPerUnit)){importer.spritePixelsPerUnit=pixelsPerUnit;changed=true;}
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);if(settings.spriteAlignment!=(int)SpriteAlignment.Center){settings.spriteAlignment=(int)SpriteAlignment.Center;importer.SetTextureSettings(settings);changed=true;}
            if(changed)importer.SaveAndReimport();
        }
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
#endif
    void RefreshHUD(){int checkpoint=checkpointX>100?2:(checkpointX>2?1:0);string powers="NONE";if(speedPowerupUntil>Time.time)powers=$"SPD {Mathf.CeilToInt(speedPowerupUntil-Time.time)}s";if(magnetPowerupUntil>Time.time)powers=powers=="NONE"?$"MAG {Mathf.CeilToInt(magnetPowerupUntil-Time.time)}s":$"{powers} + MAG {Mathf.CeilToInt(magnetPowerupUntil-Time.time)}s";if(!hud||hudScore==score&&hudCoins==coins&&hudLives==lives&&hudCheckpoint==checkpoint&&hudPowerups==powers)return;hudScore=score;hudCoins=coins;hudLives=lives;hudCheckpoint=checkpoint;hudPowerups=powers;string checkpointName=checkpoint==2?"2":(checkpoint==1?"1":"START");string powerColor=powers=="NONE"?"#EAF1E8":"#8DE0C4";hud.text=$"<color=#DCE8D8>SCORE</color> <color=#FFD66D>{score:N0}</color>   <color=#7FA38B>|</color>   <color=#DCE8D8>COINS</color> <color=#FFD66D>{coins} / {totalCoins}</color>   <color=#7FA38B>|</color>   <color=#DCE8D8>LIVES</color> <color=#FFD66D>{lives}</color>   <color=#7FA38B>|</color>   <color=#DCE8D8>CHECKPOINT</color> <color=#FFD66D>{checkpointName}</color>   <color=#7FA38B>|</color>   <color=#DCE8D8>POWER</color> <color={powerColor}>{powers}</color>";}
    GameObject UIImage(Transform parent,string name,Vector2 size,Vector2 pos,Color color,int shape){var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=pos;var im=g.GetComponent<Image>();im.sprite=MakeSprite(Color.white,shape);im.color=color;im.type=Image.Type.Simple;im.raycastTarget=false;return g;}
    Text Label(Transform parent,string s,int size,Vector2 anchor,Vector2 pivot,Vector2 pos,Color color){var g=new GameObject(s,typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);var rt=(RectTransform)g.transform;rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot;rt.anchoredPosition=pos;rt.sizeDelta=new Vector2(1400,100);var t=g.GetComponent<Text>();t.text=s;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.fontStyle=size>=22?FontStyle.Bold:FontStyle.Normal;t.alignment=TextAnchor.MiddleCenter;t.color=color;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;return t;}
    GameObject Panel(Transform parent,string name,Color color){var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;var image=g.GetComponent<Image>();image.color=color;image.raycastTarget=false;return g;}
    void SetMenuMessage(string text,Vector2 anchor,Vector2 size,int fontSize){if(!messageText)return;messageText.text=text;var rect=messageText.rectTransform;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=size;messageText.fontSize=fontSize;messageText.resizeTextForBestFit=true;messageText.resizeTextMinSize=Mathf.Max(17,fontSize-2);messageText.resizeTextMaxSize=fontSize;messageText.horizontalOverflow=HorizontalWrapMode.Wrap;messageText.verticalOverflow=VerticalWrapMode.Truncate;messageText.lineSpacing=1.08f;}
    void AddTextShadow(Text text,Color color,Vector2 distance){if(!text)return;var shadow=text.GetComponent<Shadow>();if(!shadow)shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=color;shadow.effectDistance=distance;}
    GameObject Button(Transform parent,string caption,Vector2 p,UnityEngine.Events.UnityAction action,Vector2? customSize=null,int textSize=24){var g=new GameObject(caption+" Button",typeof(RectTransform),typeof(Image),typeof(Button),typeof(Outline));g.transform.SetParent(parent,false);var rt=(RectTransform)g.transform;rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.sizeDelta=customSize??new Vector2(330,66);rt.anchoredPosition=p;var im=g.GetComponent<Image>();im.sprite=MakeSprite(Color.white,4);im.color=Color.white;var outline=g.GetComponent<Outline>();outline.effectColor=new Color(1f,.77f,.3f,.55f);outline.effectDistance=new Vector2(1,-1);var b=g.GetComponent<Button>();b.transition=Selectable.Transition.ColorTint;b.onClick.AddListener(action);var colors=b.colors;colors.normalColor=new Color(.12f,.34f,.22f);colors.highlightedColor=new Color(.22f,.53f,.32f);colors.pressedColor=new Color(.075f,.23f,.15f);colors.selectedColor=new Color(.18f,.43f,.27f);colors.fadeDuration=.12f;b.colors=colors;UIImage(g.transform,"Gold button accent",new Vector2(92,3),new Vector2(0,rt.sizeDelta.y*.5f-7),new Color(1f,.78f,.3f,.92f),4);var t=Label(g.transform,caption,textSize,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Color(1f,.98f,.89f));t.rectTransform.sizeDelta=rt.sizeDelta-new Vector2(20,8);t.fontStyle=FontStyle.Bold;AddTextShadow(t,new Color(.015f,.045f,.026f,.88f),new Vector2(1,-1));return g;}
    void ShowStart(){playing=false;Time.timeScale=1;pendingCharacter=selectedCharacter;UpdateCharacterCards();titleText.text="JUNGLE ADVENTURE";SetMenuMessage("A lost expedition. Choose your explorer,\nthen set off into the wild.",new Vector2(.5f,.615f),new Vector2(860,82),22);SetMenuPage(MenuPage.Main);UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(scenePlayButton);}
    void StartGame(){if(menuPage!=MenuPage.Main)return;if(lives<=0){Restart();return;}var visual=player.GetComponent<JunglePlayerVisual>();var frames=GetCharacterFrames(selectedCharacter);if(visual&&frames!=null)visual.SetCharacterFrames(frames);Time.timeScale=1;playing=true;SetMenuPage(MenuPage.Hidden);}
    void PauseGame(){if(!playing)return;playing=false;Time.timeScale=0;titleText.text="PAUSED";SetMenuMessage("Take a breather, explorer.\nResume when you are ready.",new Vector2(.5f,.615f),new Vector2(860,82),22);SetMenuPage(MenuPage.Pause);UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(resumeButton);}
    void Restart(){Time.timeScale=1;startAfterReload=true;SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
    void ReturnToMainMenu(){Time.timeScale=1;startAfterReload=false;SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
    void ExitGame(){
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    void Update(){if(!player)return;var keyboard=Keyboard.current;var pad=Gamepad.current;
        if(!playing&&menuPage==MenuPage.Characters){if(keyboard!=null&&keyboard.leftArrowKey.wasPressedThisFrame||pad!=null&&pad.dpad.left.wasPressedThisFrame)CycleCharacter(-1);else if(keyboard!=null&&keyboard.rightArrowKey.wasPressedThisFrame||pad!=null&&pad.dpad.right.wasPressedThisFrame)CycleCharacter(1);}
        if(!playing&&menuPage==MenuPage.Pause&&((keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)||(pad!=null&&pad.startButton.wasPressedThisFrame)))Resume();
        if(playing&&!dead&&((keyboard!=null&&(keyboard.spaceKey.wasPressedThisFrame||keyboard.wKey.wasPressedThisFrame||keyboard.upArrowKey.wasPressedThisFrame))||(pad!=null&&pad.buttonSouth.wasPressedThisFrame)))jumpBuffer=.16f;
        if(playing){if((keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)||(pad!=null&&pad.startButton.wasPressedThisFrame))PauseGame();RefreshHUD();UpdatePowerupVisuals();if(player.transform.position.y < -10)Die();}
    }
    void FixedUpdate(){if(!playing||dead)return;var k=Keyboard.current;var pad=Gamepad.current;if(k==null&&pad==null)return;float dir=0;if(k!=null)dir+=(k.rightArrowKey.isPressed||k.dKey.isPressed?1:0)-(k.leftArrowKey.isPressed||k.aKey.isPressed?1:0);if(pad!=null){float axis=pad.leftStick.x.ReadValue();if(Mathf.Abs(axis)<.15f)axis=pad.dpad.x.ReadValue();if(Mathf.Abs(axis)>.15f)dir=axis;}dir=Mathf.Clamp(dir,-1,1);float topSpeed=(k!=null&&(k.leftShiftKey.isPressed||k.rightShiftKey.isPressed)?11.5f:8f)*(speedPowerupUntil>Time.time?1.45f:1f);float vx=Mathf.MoveTowards(body.linearVelocity.x,dir*topSpeed,(dir==0?48f:38f)*Time.fixedDeltaTime);body.linearVelocity=new Vector2(vx,body.linearVelocity.y);if(Mathf.Abs(dir)>.01f)player.transform.localScale=new Vector3(Mathf.Sign(dir),player.transform.localScale.y,1);
        bool onGround=false;int hitCount=Physics2D.Raycast(player.transform.position+Vector3.down*.48f,Vector2.down,groundFilter,groundHits,.28f);for(int i=0;i<hitCount;i++){var hit=groundHits[i];if(hit.collider&&hit.collider!=bodyCollider&&!hit.collider.isTrigger&&hit.normal.y>.45f){onGround=true;break;}}grounded=onGround;if(grounded)coyote=.12f;else coyote-=Time.fixedDeltaTime;
        jumpBuffer-=Time.fixedDeltaTime;if(jumpBuffer>0&&coyote>0){body.linearVelocity=new Vector2(body.linearVelocity.x,JumpVelocity);audio?.PlayJump();jumpBuffer=0;coyote=0;}bool jumpHeld=(k!=null&&(k.spaceKey.isPressed||k.wKey.isPressed||k.upArrowKey.isPressed))||(pad!=null&&pad.buttonSouth.isPressed);if(!jumpHeld&&body.linearVelocity.y>MinimumJumpVelocity)body.linearVelocity=new Vector2(body.linearVelocity.x,MinimumJumpVelocity);
    }
    void LateUpdate(){if(!cam||!player)return;Vector3 target=new Vector3(Mathf.Clamp(Mathf.Max(0,player.transform.position.x+2.2f),0,EndX-8),Mathf.Clamp(player.transform.position.y+.9f,-.2f,1.5f),-10);Vector3 pos=Vector3.SmoothDamp(cam.transform.position,target,ref cameraVelocity,.19f,Mathf.Infinity,Time.deltaTime);if(cameraShake>0){pos+=(Vector3)Random.insideUnitCircle*cameraShake;cameraShake=Mathf.MoveTowards(cameraShake,0,Time.unscaledDeltaTime*.8f);}cam.transform.position=new Vector3(pos.x,pos.y,-10);}
    public void Collect(GameObject g){if(!coinObjects.Contains(g))return;coinObjects.Remove(g);coins++;score+=100;audio?.PlayCoin();RefreshHUD();StartCoroutine(CoinBurst(g.transform.position));g.SetActive(false);}
    public void ActivatePowerup(JunglePowerupKind kind){float expiry=Time.time+10f;switch(kind){case JunglePowerupKind.Speed:speedPowerupUntil=expiry;break;case JunglePowerupKind.Magnet:magnetPowerupUntil=expiry;break;}audio?.PlayPowerup();RefreshHUD();}
    void UpdatePowerupVisuals(){if(magnetPowerupUntil<=Time.time||!player)return;Vector2 target=player.transform.position+Vector3.up*.2f;const float pullRadius=5.5f,pullSpeed=8f;for(int i=coinObjects.Count-1;i>=0;i--){var coin=coinObjects[i];if(!coin||!coin.activeInHierarchy)continue;Vector2 coinPosition=coin.transform.position;if(Vector2.Distance(coinPosition,target)>pullRadius)continue;coin.transform.position=Vector2.MoveTowards(coinPosition,target,pullSpeed*Time.deltaTime);}}
    IEnumerator CoinBurst(Vector3 at){
        if(sceneCoinSparkles==null||sceneCoinSparkles.Length==0)yield break;
        int count=Mathf.Min(9,sceneCoinSparkles.Length);var dirs=new Vector2[count];for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;dirs[i]=new Vector2(Mathf.Cos(a),Mathf.Sin(a));if(sceneCoinSparkles[i]){sceneCoinSparkles[i].transform.position=at;sceneCoinSparkles[i].transform.localScale=Vector3.one;sceneCoinSparkles[i].SetActive(true);}}
        float elapsed=0;while(elapsed<.38f){for(int i=0;i<count;i++)if(sceneCoinSparkles[i]){sceneCoinSparkles[i].transform.position=at+(Vector3)(dirs[i]*elapsed*3.5f);sceneCoinSparkles[i].transform.localScale=Vector3.one*Mathf.Lerp(1,.05f,elapsed/.38f);}elapsed+=Time.unscaledDeltaTime;yield return null;}
        foreach(var sparkle in sceneCoinSparkles)if(sparkle){sparkle.SetActive(false);sparkle.transform.localScale=Vector3.one;}
    }
    public void Die(){if(dead||!playing)return;dead=true;lives--;audio?.PlayDamage();RefreshHUD();body.linearVelocity=Vector2.zero;body.simulated=false;jumpBuffer=0;coyote=0;player.GetComponent<JunglePlayerVisual>()?.SetDead(true);StartCoroutine(CameraImpact());if(lives<=0){playing=false;audio?.PlayGameOver();titleText.text="GAME OVER";SetMenuMessage($"Final score: {score:N0}\nSun coins collected: {coins} of {totalCoins}",new Vector2(.5f,.615f),new Vector2(860,96),22);SetMenuPage(MenuPage.GameOver);UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(sceneRestartButton);return;}StartCoroutine(Respawn());}
    IEnumerator CameraImpact(){cameraShake=.14f;while(cameraShake>.005f){yield return null;}cameraShake=0;}
    IEnumerator Respawn(){yield return new WaitForSecondsRealtime(.55f);player.transform.position=new Vector2(checkpointX,-2.36f);body.linearVelocity=Vector2.zero;body.simulated=true;jumpBuffer=0;coyote=0;grounded=false;dead=false;player.GetComponent<JunglePlayerVisual>()?.SetDead(false);}
    public void Checkpoint(float positionX){if(positionX<=checkpointX+1f)return;checkpointX=positionX;audio?.PlayCheckpoint();RefreshHUD();}
    public void Finish(){if(won)return;won=true;playing=false;audio?.PlayFinish();titleText.text="TREASURE FOUND!";SetMenuMessage($"Final score: {score:N0}\nSun coins collected: {coins} of {totalCoins}",new Vector2(.5f,.615f),new Vector2(860,96),22);SetMenuPage(MenuPage.Victory);UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(sceneRestartButton);}
    void Resume(){Time.timeScale=1;playing=true;SetMenuPage(MenuPage.Hidden);}
}
