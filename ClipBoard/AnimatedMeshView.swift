import SwiftUI

struct AnimatedMeshView: View {
    let colors: [Color]
    @State private var animate = false

    var body: some View {
        GeometryReader { geometry in
            ZStack {
                // Background Base
                colors.last?.ignoresSafeArea()

                // Moving Blob 1
                Circle()
                    .fill(colors.first ?? .blue)
                    .frame(width: geometry.size.width * 0.8, height: geometry.size.width * 0.8)
                    .blur(radius: 60)
                    .offset(x: animate ? -100 : 100, y: animate ? -50 : 50)
                    .animation(
                        .easeInOut(duration: 5.0).repeatForever(autoreverses: true),
                        value: animate
                    )

                // Moving Blob 2
                Circle()
                    .fill(colors.count > 1 ? colors[1] : .gray)
                    .frame(width: geometry.size.width * 0.7, height: geometry.size.width * 0.7)
                    .blur(radius: 60)
                    .offset(x: animate ? 150 : -150, y: animate ? 100 : -100)
                    .animation(
                        .easeInOut(duration: 7.0).repeatForever(autoreverses: true),
                        value: animate
                    )
            }
            .onAppear {
                animate.toggle()
            }
        }
        .drawingGroup() // Improves performance
        .ignoresSafeArea()
    }
}
