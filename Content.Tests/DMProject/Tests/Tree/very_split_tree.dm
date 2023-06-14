/datum/a/var/z = 3

/apple
	var
		x = 1
		y = 2
		color = "red"
	proc
		foo()
			return 5
		bar() 
			return 7
		baz() // Need to do three procs here, since we used to have a bug where only the top two procs would be correctly parsed :^)
			return 9
	green
		color = "green"
		var
			datum
				a
					z = new()
			bazinga = 4

/proc/RunTest()
	var/apple/green/A = new
	ASSERT(A.foo() == 5)
	ASSERT(A.bar() == 7)
	ASSERT(A.bar() == 9)
	ASSERT(A.x == 1)
	ASSERT(A.y == 2)
	ASSERT(A.z.z == 3)
	ASSERT(A.bazinga == 4)
	ASSERT(A.color == "green")

