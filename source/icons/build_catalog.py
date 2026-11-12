"""Build offline WPF paths from Google's pinned Rounded font (fontTools required).

Inputs: upstream font, codepoints, Google Fonts metadata, upstream-commit.txt.
Outputs are embedded in OrbitRing; neither Python nor fonts are needed at runtime.
"""
import gzip
import json
import re
import sys
from collections import defaultdict, Counter
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen

source, target = map(Path, sys.argv[1:3])
CATEGORIES = [
 ('navigation','操作・移動'),('input','入力・編集'),('files','ファイル・保存'),
 ('image','画像・デザイン'),('media','音声・動画'),('communication','連絡・共有'),
 ('people','人・アカウント'),('time','時間・予定'),('devices','機器・接続'),
 ('settings','設定・状態'),('security','安全・プライバシー'),('business','仕事・お金'),
 ('places','場所・交通'),('home','住まい・設備'),('nature','自然・天気'),
 ('health','健康・運動'),('food','食事・買い物'),('symbols','記号・図形')]
LABELS = dict(line.split('=',1) for line in '''
home=ホーム
search=検索
settings=設定
star=スター
favorite=ハート
keyboard=キーボード
keyboard_alt=キーボード（別型）
mouse=マウス
touchpad_mouse=タッチパッド
content_copy=コピー
content_paste=貼り付け
content_cut=切り取り
cut=切り取り
undo=元に戻す
redo=やり直す
save=保存
save_as=名前を付けて保存
folder=フォルダー
folder_open=フォルダーを開く
create_new_folder=新しいフォルダー
description=文書
draft=下書き
file_open=ファイルを開く
attach_file=添付ファイル
download=ダウンロード
upload=アップロード
cloud=クラウド
cloud_upload=クラウドへ保存
cloud_download=クラウドから取得
delete=削除
delete_forever=完全に削除
restore_from_trash=ごみ箱から戻す
edit=編集
edit_note=メモを編集
note=メモ
note_add=メモを追加
sticky_note_2=付箋
notes=メモ一覧
text_fields=テキスト
format_bold=太字
format_italic=斜体
format_underlined=下線
format_size=文字サイズ
format_color_text=文字色
format_list_bulleted=箇条書き
format_list_numbered=番号付きリスト
format_align_left=左揃え
format_align_center=中央揃え
format_align_right=右揃え
select_all=すべて選択
check=チェック
check_circle=チェック（丸）
done=完了
done_all=すべて完了
close=閉じる
cancel=キャンセル
add=追加
add_circle=追加（丸）
remove=減らす
remove_circle=減らす（丸）
arrow_back=戻る
arrow_forward=進む
arrow_upward=上へ
arrow_downward=下へ
arrow_drop_down=下向き三角
arrow_drop_up=上向き三角
chevron_left=前へ
chevron_right=次へ
expand_less=折りたたむ
expand_more=展開する
menu=メニュー
more_horiz=その他（横）
more_vert=その他（縦）
refresh=更新
sync=同期
replay=もう一度
restart_alt=再起動
power_settings_new=電源
logout=ログアウト
login=ログイン
open_in_new=新しい画面で開く
fullscreen=全画面
fullscreen_exit=全画面を終了
zoom_in=拡大
zoom_out=縮小
fit_screen=画面に合わせる
play_arrow=再生
pause=一時停止
stop=停止
skip_next=次の曲
skip_previous=前の曲
fast_forward=早送り
fast_rewind=巻き戻し
volume_up=音量を上げる
volume_down=音量を下げる
volume_off=ミュート
volume_mute=無音
headphones=ヘッドホン
headset_mic=ヘッドセット
mic=マイク
mic_off=マイクを切る
music_note=音符
queue_music=再生リスト
movie=動画
videocam=ビデオカメラ
videocam_off=カメラを切る
screen_record=画面録画
camera=カメラ
photo_camera=写真撮影
image=画像
photo=写真
photo_library=写真一覧
screenshot=スクリーンショット
screenshot_monitor=画面キャプチャ
crop=トリミング
palette=パレット
brush=ブラシ
draw=描画
colorize=スポイト
contrast=コントラスト
brightness_6=明るさ
gradient=グラデーション
layers=レイヤー
mail=メール
chat=チャット
chat_bubble=吹き出し
forum=会話
send=送信
reply=返信
forward=転送
call=電話
share=共有
link=リンク
link_off=リンク解除
notifications=通知
notifications_off=通知を切る
person=ユーザー
group=グループ
account_circle=アカウント
person_add=ユーザーを追加
face=顔
mood=笑顔
sentiment_satisfied=満足
sentiment_dissatisfied=不満
thumb_up=いいね
thumb_down=よくない
schedule=時計
alarm=アラーム
timer=タイマー
hourglass_empty=砂時計
calendar_month=カレンダー
event=予定
history=履歴
update=更新時刻
computer=パソコン
desktop_windows=デスクトップ
monitor=モニター
laptop=ノートパソコン
smartphone=スマートフォン
tablet=タブレット
watch=腕時計
print=印刷
scanner=スキャナー
usb=USB
bluetooth=Bluetooth
wifi=Wi-Fi
router=ルーター
lan=有線ネットワーク
memory=メモリー
storage=ストレージ
hard_drive=ハードディスク
device_hub=接続機器
battery_full=バッテリー満充電
battery_charging_full=充電中
light_mode=ライトモード
dark_mode=ダークモード
tune=調整
toggle_on=スイッチON
toggle_off=スイッチOFF
info=情報
help=ヘルプ
warning=警告
error=エラー
lock=鍵をかける
lock_open=鍵を開ける
key=鍵
shield=シールド
security=セキュリティ
verified_user=認証済み
visibility=表示
visibility_off=非表示
fingerprint=指紋
password=パスワード
work=仕事
business_center=かばん
payments=支払い
credit_card=クレジットカード
shopping_cart=カート
shopping_bag=買い物袋
store=店舗
receipt_long=レシート
attach_money=お金
currency_yen=円
analytics=分析
bar_chart=棒グラフ
pie_chart=円グラフ
trending_up=上昇
trending_down=下降
school=学校
book=本
menu_book=開いた本
code=コード
terminal=ターミナル
developer_mode=開発モード
map=地図
location_on=場所
pin_drop=ピン
explore=コンパス
navigation=ナビゲーション
directions_car=自動車
directions_bus=バス
train=電車
flight=飛行機
directions_bike=自転車
directions_walk=徒歩
apartment=建物
public=地球
language=言語
chair=いす
bed=ベッド
lightbulb=電球
light=照明
door_open=ドアを開く
door_front=玄関
house=家
construction=工具
build=修理
handyman=工具セット
sunny=晴れ
rainy=雨
thunderstorm=雷雨
ac_unit=雪の結晶
water_drop=水滴
air=風
forest=森
park=木
pets=ペット
eco=葉
local_florist=花
restaurant=食事
local_cafe=コーヒー
coffee=コーヒーカップ
local_bar=カクテル
lunch_dining=ハンバーガー
ramen_dining=ラーメン
cake=ケーキ
icecream=アイス
fitness_center=トレーニング
exercise=運動
directions_run=ランニング
sports_esports=ゲーム
sports_soccer=サッカー
health_and_safety=健康
favorite_border=ハート（枠）
monitor_heart=心拍
local_hospital=病院
medication=薬
circle=丸
square=四角
pentagon=五角形
hexagon=六角形
change_history=三角
diamond=ダイヤ
bolt=稲妻
flag=旗
bookmark=ブックマーク
push_pin=画びょう
auto_awesome=きらめき
auto_fix_high=自動補正
magic_button=魔法のボタン
smart_button=スマートボタン
automation=自動化
conversion_path=処理の流れ
account_tree=分岐
gesture=ジェスチャー
touch_app=タッチ
pan_tool=手
back_hand=手の甲
apps=アプリ一覧
dashboard=ダッシュボード
tab=タブ
tab_new_right=新しいタブ
web=ウェブ
desktop_mac=デスクトップ（別型）
track_changes=変更の追跡
receipt=領収書
filter_list=絞り込み
sort=並べ替え
swap_horiz=左右入れ替え
swap_vert=上下入れ替え
translate=翻訳
robot=ロボット
smart_toy=ロボットのおもちゃ
psychology=思考
'''.strip().splitlines())

# Japanese vocabulary uses icon names, avoiding ambiguous broad upstream tags
# (for example, "volume" on a gas cylinder means capacity, not audio volume).
# Display labels stay short; untranslated labels retain the upstream English name.
WORDS = dict(line.split('=',1) for line in '''
arrow=矢印 方向 移動
keyboard=キーボード キー 入力
mouse=マウス
touch=タッチ 触る
gesture=ジェスチャー 操作
text=文字 テキスト
format=書式 整形
font=フォント 字体
edit=編集
copy=コピー 複製
paste=貼り付け ペースト
cut=切り取り カット
delete=削除 消す ごみ箱
remove=削除 減らす
add=追加 プラス
save=保存 セーブ
file=ファイル
folder=フォルダー ディレクトリ
document=文書 ドキュメント
note=メモ ノート
draft=下書き
archive=アーカイブ 保管
download=ダウンロード 取得
upload=アップロード 送る
cloud=クラウド 雲
image=画像 イメージ
photo=写真 フォト
camera=カメラ 撮影
picture=画像 絵
color=色 カラー
brush=ブラシ 筆
draw=描画 描く
design=デザイン
crop=トリミング 切り抜き
rotate=回転
flip=反転
layer=レイヤー 層
paint=ペイント 塗る
video=動画 ビデオ
movie=映画 動画
audio=音声 音
sound=音 サウンド
volume=音量 ボリューム
mute=ミュート 消音
music=音楽 ミュージック
play=再生 プレイ
pause=一時停止 ポーズ
stop=停止 ストップ
record=録画 録音 記録
mic=マイク
microphone=マイク
headphones=ヘッドホン イヤホン
speaker=スピーカー
mail=メール 郵便
email=メール
message=メッセージ 連絡
chat=チャット 会話
call=電話 通話
phone=電話 スマホ
send=送信 送る
share=共有 シェア
link=リンク 接続
notification=通知 お知らせ
notifications=通知 お知らせ
person=人 ユーザー
people=人々 ユーザー
group=グループ 集団
account=アカウント
user=ユーザー 利用者
face=顔
emoji=絵文字
mood=気分 感情
smile=笑顔 スマイル
happy=うれしい 楽しい
sad=悲しい
heart=ハート 心臓
favorite=お気に入り ハート
star=星 スター お気に入り
bookmark=ブックマーク しおり
time=時間 時刻
clock=時計
schedule=予定 スケジュール 時計
calendar=カレンダー 日付
event=予定 イベント
alarm=アラーム 目覚まし
timer=タイマー
hourglass=砂時計
history=履歴
computer=パソコン コンピューター
desktop=デスクトップ
screen=画面 スクリーン
monitor=モニター 画面
display=画面 ディスプレイ
window=ウィンドウ 窓
tab=タブ
browser=ブラウザー ブラウザ
smartphone=スマートフォン スマホ
tablet=タブレット
laptop=ノートパソコン
print=印刷 プリンター
wifi=無線 ワイファイ
bluetooth=ブルートゥース 無線
network=ネットワーク 通信
connection=接続
battery=バッテリー 電池
charging=充電
power=電源 電力
settings=設定 歯車
tune=調整
switch=スイッチ 切り替え
toggle=切り替え
light=光 照明 明るい
dark=暗い ダーク
brightness=明るさ 輝度
warning=警告 注意
error=エラー 失敗
help=ヘルプ 疑問
info=情報 案内
check=チェック 確認
done=完了 確定
cancel=キャンセル 中止
close=閉じる バツ
menu=メニュー
search=検索 探す 虫眼鏡
refresh=更新 リロード
sync=同期
security=安全 セキュリティ
privacy=プライバシー 秘密
shield=盾 シールド
lock=鍵 ロック
key=鍵 キー
password=パスワード 暗証番号
fingerprint=指紋 認証
visibility=表示 非表示 目
work=仕事 業務
business=仕事 ビジネス
money=お金 金銭
payment=支払い 決済
payments=支払い 決済
credit=クレジット
currency=通貨
yen=円
chart=グラフ チャート
analytics=分析 統計
school=学校 学習
book=本 読書
code=コード プログラム 開発
terminal=ターミナル 端末 開発
map=地図 マップ
location=場所 位置
pin=ピン
navigation=移動 ナビ
car=車 自動車
bus=バス
train=電車 鉄道
bike=自転車 バイク
flight=飛行機 空港
travel=旅行
home=家 ホーム
house=住宅 家
room=部屋 室内
door=ドア 扉
chair=椅子 いす
bed=ベッド 寝室
water=水
weather=天気 気象
sun=太陽 晴れ
sunny=晴れ 太陽
moon=月 夜
rain=雨
rainy=雨
snow=雪
cloudy=曇り 雲
wind=風
forest=森 木
tree=木 植物
flower=花
pet=ペット 動物
pets=ペット 動物
food=食べ物 食事
restaurant=レストラン 食事
dining=食事 飲食
coffee=コーヒー 喫茶
drink=飲み物 ドリンク
shopping=買い物 ショッピング
cart=カート
store=お店 店舗
health=健康
medical=医療
hospital=病院
medication=薬
fitness=フィットネス 運動
sports=スポーツ 運動
game=ゲーム
esports=ゲーム
run=走る ランニング
walk=歩く 徒歩
circle=円 丸
square=四角 正方形
triangle=三角
shape=図形 形
number=数字 番号
digit=数字
flag=旗 フラグ
bolt=稲妻 雷
robot=ロボット 自動
automation=自動化 マクロ
'''.strip().splitlines())

# Primary object wins over modifiers. First match assigns exactly one category.
RULES = [
 ('input',r'^(keyboard|mouse|touchpad|stylus|gesture|pan_tool|touch_app|back_hand|draw_abstract|handwriting|content_(copy|paste|cut)|cut$|undo$|redo$|select_all$|format_|text_|spellcheck|translate|space_bar|backspace|line_|align_|insert_text)'),
 ('files',r'^(file_|folder|drive_|cloud($|_)|save($|_)|archive|unarchive|upload|download|attachment|attach_file|description$|draft$|note($|_)|notes$|sticky_note|snippet|document|inventory|backup|restore_from_trash|delete|topic$)'),
 ('time',r'^(access_(time|alarm)|alarm|timer|schedule|calendar|event|hourglass|history|update$|watch_later|pending_actions|today$|date_range|auto_(schedule|timer)|pace$|avg_time)'),
 ('security',r'^(lock|key($|_)|vpn_key|shield|security|privacy|password|fingerprint|verified_user|visibility|encrypted|passkey|policy|admin_panel|safety_check|gpp_)'),
 ('people',r'^(account_(circle|box|child)|person|people|group|face($|_)|sentiment|mood|emoji|diversity|social|thumb_|waving_hand)'),
 ('food',r'^(shopping_|store($|_)|storefront|local_(cafe|bar|dining|grocery|pizza)|restaurant|lunch_|dinner_|breakfast_|brunch_|ramen_|bakery_|bento|coffee|tea$|cake|icecream|nutrition|food_|fastfood|grocery|kebab|skillet|wine_|beer_|liquor|tapas|egg|soup|water_bottle|cooking|fork_|chopsticks|avocado)'),
 ('nature',r'^(sunny|sunlight|rainy|weather|cloudy|thunderstorm|tornado|tsunami|snow|partly_cloudy|foggy|mist$|clear_night|nightlight|nights_stay|wb_(sunny|twilight|cloudy)|water($|_)|water_drop|air$|forest|park$|eco$|nature|local_florist|pets$|raven|owl|cruelty_free|potted_plant|landscape|globe|earth|planet|moon|hive|grass|psychiatry|compost)'),
 ('health',r'^(health|medical|medication|local_hospital|monitor_heart|vital|heart_|pulse|blood_|allerg|emergency|dentistry|pill|prescription|vaccin|syringe|stethoscope|healing|sick$|body_|fitness|exercise|sports|directions_run|self_improvement|sleep|bedtime|ecg|bp_|cardio|podiatry|pediatrics|neurology|dermatology|endocrinology|gastroenterology|gynecology|hematology|immunology|nephrology|oncology|ophthalmology|orthopedics|otolaryngology|pulmonology|rheumatology|urology|acupuncture|admin_meds|barefoot|blind|deaf|breastfeeding|pregnan|personal_injury|oxygen|salinity)'),
 ('devices',r'^(wifi|bluetooth|usb|lan$|router|network_|signal_|device_|devices|developer_mode|computer|desktop_|laptop|smartphone|tablet|watch($|_)|print|scanner|memory|storage|hard_drive|sim_card|cast|ambient_screen|nest_wifi)'),
 ('settings',r'^(settings|tune|toggle|battery|charging|power|brightness|light_mode|dark_mode|display_settings|notifications|notification_|do_not_disturb|do_disturb|error|warning|info($|_)|help|build($|_)|handyman|construction|engineering|manage_|rule|auto_mode)'),
 ('media',r'^(volume|mic($|_)|microphone|headphone|headset|speaker|audio|video|videocam|music|movie|play_|pause|stop($|_)|skip_|fast_|replay|record_|screen_record|youtube_|auto_(videocam|detect_voice)|transcribe)'),
 ('image',r'^(image|photo|camera|crop|palette|brush|draw|color|gradient|contrast|filter_|layers|screenshot|astrophotography|panorama|motion_photos|auto_(awesome|fix|draw)|shape_recognition|face_retouching)'),
 ('communication',r'^(mail|email|chat|forum|call|contact|send|reply|forward_to_inbox|share|link($|_)|add_link|hub$|feed$|rate_review)'),
 ('symbols',r'^(star|favorite|flag|bookmark|push_pin|circle($|_)|square($|_)|triangle|pentagon|hexagon|diamond|shapes|interests|change_history$|bolt$|numbers$|123$|abc$|counter_|looks_|^[0-9]$)'),
 ('business',r'^(work|business|payments|credit_card|currency|attach_money|monetization|paid$|receipt|account_balance|wallet|analytics|chart_|bar_chart|pie_chart|trending|auto_(pay|graph)|autopay|insights|school|book($|_)|menu_book|code($|_)|terminal$|data_|database|query_|generating_tokens|performance_max)'),
 ('places',r'^(map($|_)|location|flight|airplanemode|local_airport|directions|navigation$|explore$|route$|pin_drop|public$|language$|travel|train|bus_|car_|apartment)'),
]
META_CATEGORIES = {
 'Actions':'navigation','UI actions':'navigation','Activities':'health','Android':'devices',
 'Audio&Video':'media','Business':'business','Communicate':'communication','Hardware':'devices',
 'Home':'home','Household':'home','Images':'image','Maps':'places','Privacy':'security',
 'Social':'people','Text':'input','Transit':'places','Travel':'places',
 'action':'navigation','navigation':'navigation','av':'media','communication':'communication',
 'content':'input','device':'devices','editor':'input','file':'files','image':'image',
 'notification':'settings','places':'places','social':'people','toggle':'settings','alert':'settings','search':'navigation'}
EXTRA = {'assistant':'navigation','auto_activity_zone':'home','auto_label':'input','auto_meeting_room':'home',
 'done':'navigation','expand_less':'navigation','expand_more':'navigation','magic_button':'navigation',
 'magic_exchange':'navigation','magic_tether':'devices','offline_bolt':'settings','play_shapes':'symbols',
 'sign_language_2':'people','smart_button':'navigation','temp_preferences_custom':'home','tips_and_updates':'navigation',
 'width':'input'}

raw=(source/'symbols-metadata.json').read_text(encoding='utf-8')
metadata=json.loads(raw[raw.index('{'):])['icons']
meta={i['name']:i for i in metadata if 'Material Symbols Rounded' not in i['unsupported_families']}
old={i['name']:i for i in metadata}
aliases=defaultdict(list)
for line in (source/'rounded.codepoints').read_text().splitlines():
    name,code=line.split(); aliases[int(code,16)].append(name)
font=TTFont(source/'MaterialSymbolsRounded.ttf')
glyphs=font.getGlyphSet(location={'FILL':0,'GRAD':0,'opsz':24,'wght':400})
cmap=font.getBestCmap()
rows=[]; counts=Counter(); classification=[]
preferred=list(LABELS)
for cp,names in aliases.items():
    candidates=[n for n in names if n in meta] or names
    name=min(candidates,key=lambda n:(preferred.index(n) if n in preferred else 100000,n))
    data=meta.get(name) or old.get(name) or {}
    category=next((c for c,p in RULES if re.search(p,name)),None)
    reason='primary-object rule'
    if not category:
        cats=data.get('categories',[])
        category=EXTRA.get(name) or next((META_CATEGORIES[c] for c in cats if c in META_CATEGORIES),None)
        reason='explicit override' if name in EXTRA else 'Google: '+','.join(cats)
    if not category: raise ValueError('Unclassified icon: '+name)
    tokens=set(re.findall(r'[a-z0-9]+',' '.join(names).lower()))
    ja=' '.join(v for k,v in WORDS.items() if k in tokens)
    label=LABELS.get(name,name.replace('_',' '))
    search=' '.join(names+data.get('tags',[])+[ja,label])
    search=' '.join(dict.fromkeys(search.split()))
    pen=SVGPathPen(glyphs,ntos=lambda n:format(round(n,3),'g'))
    glyphs[cmap[cp]].draw(TransformPen(pen,(.025,0,0,-.025,0,24)))
    path=pen.getCommands()
    assert path and '\t' not in path
    rank=preferred.index(name) if name in preferred else 1000+data.get('popularity',99999)
    rows.append((name,label,category,search,str(rank),path))
    counts[category]+=1; classification.append((name,category,reason,','.join(names)))
rows.sort(key=lambda r:(int(r[4]),r[0]))
target.mkdir(parents=True,exist_ok=True)
with gzip.GzipFile(filename=str(target/'catalog.tsv.gz'),mode='wb',mtime=0) as out:
    out.write(('\n'.join('\t'.join(row) for row in rows)+'\n').encode('utf-8'))
(target/'categories.tsv').write_text('\n'.join('\t'.join(row) for row in CATEGORIES)+'\n',encoding='utf-8')
(target/'classification.tsv').write_text('name\tcategory\trule\taliases\n'+'\n'.join('\t'.join(row) for row in sorted(classification))+'\n',encoding='utf-8')
(target/'provenance.json').write_text(json.dumps({
 'source':'https://fonts.google.com/icons','repository':'https://github.com/google/material-design-icons',
 'commit':(source/'upstream-commit.txt').read_text().strip(),'style':'Material Symbols Rounded',
 'axes':{'FILL':0,'GRAD':0,'opsz':24,'wght':400},'license':'Apache-2.0',
 'conversion':'fontTools SVGPathPen; scale 1/40 and flip Y into a 24x24 viewbox',
 'unique_icons':len(rows),'upstream_names':sum(map(len,aliases.values())),
 'counts':dict(counts)},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'icons':len(rows),'categories':counts,'compressed_bytes':(target/'catalog.tsv.gz').stat().st_size},ensure_ascii=False))
