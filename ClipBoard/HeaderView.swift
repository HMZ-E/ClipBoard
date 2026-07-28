import SwiftUI

struct HeaderView: View {
    @Binding var showSettings: Bool

    var body: some View {
        HStack {
            // FIX: Tries to load the logo image first.
            if let _ = NSImage(named: "ClipboardLogo") {
                Image("ClipboardLogo")
                    .resizable()
                    .scaledToFit()
                    .frame(height: 40) // Adjust height as needed
            } else {
                // Fallback text if image isn't found
                Text("CLIPBOARD")
                    .font(.custom("Futura", size: 28)) // Metallic style font
                    .bold()
                    .foregroundStyle(
                        LinearGradient(
                            colors: [.white, .gray, .white],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )
            }

            Spacer()

            Button {
                showSettings = true
            } label: {
                Image(systemName: "gearshape.fill")
                    .foregroundColor(.white.opacity(0.8))
                    .font(.system(size: 16))
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 4)
    }
}
