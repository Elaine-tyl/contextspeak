/** Original CSS geometry: no remote models, textures or rendering engine. */
export function LearningSculpture() {
  return (
    <div className="learning-sculpture" aria-hidden="true" data-depth>
      <div className="sculpture-world">
        <div className="sculpture-plinth" />
        <div className="sculpture-orbit">
          {[
            ["brand", "C"],
            ["speak", "♫"],
            ["review", "✦"],
          ].map(([tone, glyph]) => (
            <div key={tone} className={`sculpture-cube cube-${tone}`}>
              {["front", "back", "left", "right", "top", "bottom"].map(
                (face) => (
                  <span key={face} className={`cube-face cube-${face}`}>
                    {face === "top" || face === "front" ? glyph : ""}
                  </span>
                ),
              )}
            </div>
          ))}
        </div>
        <span className="sculpture-dot dot-one" />
        <span className="sculpture-dot dot-two" />
      </div>
    </div>
  );
}
