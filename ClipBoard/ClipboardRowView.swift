import SwiftUI

struct ClipboardRowView: View {

    let item: ClipboardItem

    var body: some View {
        VStack(alignment: .leading) {
            switch item.content {
            case .text(let str):
                Text(str)
                    .lineLimit(3)

            case .image(let data):
                if let nsImage = NSImage(data: data) {
                    Image(nsImage: nsImage)
                        .resizable()
                        .scaledToFit()
                        .frame(height: 100)
                }
            }
        }
        .padding()
        .background(.ultraThinMaterial)
        .cornerRadius(16)
    }
}
