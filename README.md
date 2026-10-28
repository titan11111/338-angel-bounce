# ANGEL BOUNCE — Day 104

Unity Hubの登録先：このProjectフォルダ。Unity 6000.3.26f1。
Assets/Scenes/Main.unityを開きPlayするとタイトル画面が表示される。

## 操作
- WASD/矢印：天使を移動、Q/E：板の回転、Space：投げる、R：再挑戦。
- 画面左スティックと回転・投球・再挑戦ボタンでも同じ操作。
- Gamepad：左スティック/十字キー、LB/RBまたはLT/RT、A/×で投球、B/○で再挑戦。
- タイトルはSTART、Space/Enter、Gamepad A/Startで開始。
- 音ボタンでミュートを切り替え、状態を保存。

## 検証・書き出し
- Titan > Verify Gameplay：実際のランタイムコードをPlayモードで検証。../QA/gameplay-checks.txtへ結果を保存。
- Titan > Build iOS Xcode：../Builds/iOSへ出力。Unity-iPhone.xcodeprojをXcodeで開き、実機へ入れる場合は自分のSigning Teamと接続端末を指定。
- Titan > Build Mac：../Builds/Mac/AngelBounce.app。
- Titan > Build Web：../Builds/Web。HTTP配信で確認する。Webの日本語フォントとiPhone/Androidブラウザは追加検証が必要。

## 音楽
| 場面 | ユーザー提供ファイル |
| --- | --- |
| タイトル | Before_the_Sun_Breaks.mp3 |
| ゲーム | Running_Toward_the_Sun.mp3 |
| 成功・失敗エンディング | A_Heart_in_a_Quiet_Room.mp3 |

長い音楽はストリーミング、短い「ぼよーん」音声と効果音は別AudioSource。ネット配信は使わない。

## 現在の確認範囲
C#コンパイル成功、Editorで39項目PASS。iOS Xcodeエクスポート成功（../Builds/iOS/Unity-iPhone.xcodeproj）。実際のマウス/キーボード操作でも投球→反射→成功、移動して失敗、両エンディングとタイトルを確認。
Gamepadと複数ポインターは仮想入力によるPlayモード検証。Bluetooth実機コントローラ、iPhone/Android実機は未確認。
