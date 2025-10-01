# lng_pdf_extract_images ツールのテスト

## クイックテスト

### テスト 1: 必須パラメータの欠落
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{}'
```
**期待される結果:** pdf_pathが必須であることを示すエラーメッセージ

### テスト 2: ファイルが見つからない
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"nonexistent.pdf"}'
```
**期待される結果:** フルパスでファイルが見つからないことを示すエラーメッセージ

### テスト 3: 無効な画像フォーマット
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"test.pdf","image_format":"gif"}'
```
**期待される結果:** 無効なフォーマットであり、png または jpeg でなければならないことを示すエラーメッセージ

### テスト 4: 画像を含む有効なPDF（PNG形式）
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"path/to/document.pdf"}'
```
**期待される結果:** success=true のJSONと、メタデータ付きの抽出された画像のリスト

### テスト 5: 画像を含む有効なPDF（JPEG形式）
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"path/to/document.pdf","image_format":"jpeg"}'
```
**期待される結果:** success=true のJSONと、JPEG形式で保存された画像

### テスト 6: カスタム出力ディレクトリを持つ有効なPDF
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"path/to/document.pdf","output_dir":"./extracted"}'
```
**期待される結果:** success=true のJSONと、./extractedディレクトリに保存された画像

### テスト 7: 画像のないPDF
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"path/to/text-only.pdf"}'
```
**期待される結果:** PDFに画像が見つからないことを示すエラーメッセージ

## Jiraワークフローとの統合テスト

### 完全なワークフローテスト
1. JiraチケットからPDFをダウンロード:
```bash
dotnet run --project Run.csproj -- run lng_jira_download_attachments '{"ticket_number":"PROJ-123","output_directory":"./work/PROJ-123"}'
```

2. PDFから画像を抽出:
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"./work/PROJ-123/document.pdf","output_dir":"./work/PROJ-123/images"}'
```

3. Jiraに画像をアップロード:
```bash
dotnet run --project Run.csproj -- run lng_jira_upload_attachment '{"ticket_number":"PROJ-123","file_path":"./work/PROJ-123/images/image_001.png"}'
```

## テスト結果サマリー

✅ **テスト 1（パラメータ欠落）:** 合格 - 適切なエラーを返す
✅ **テスト 2（ファイルが見つからない）:** 合格 - ファイルが見つからないエラーを返す
✅ **テスト 3（無効なフォーマット）:** 合格 - フォーマット検証エラーを返す
⏸️ **テスト 4-7:** テストには実際の画像を含むPDFファイルが必要

## 注意事項
- すべての検証とエラーハンドリングのテストが正常に合格しました
- 画像抽出機能には、埋め込まれた画像を含む実際のPDFファイルが必要です
- ツールはMCPシステムに適切に統合されています
- Run.csproj と mcp.csproj の両方が必要な依存関係で正常にビルドされます
