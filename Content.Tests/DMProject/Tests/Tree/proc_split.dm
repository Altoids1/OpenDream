/proc
	foo()
		return 7
	bar()
		return 9

/apple
	proc
		color()
			return "red"
		taste()
			return "good"
		smell()
			return "cider"

/proc/RunTest()
	ASSERT(foo() == 7)
	ASSERT(bar() == 9)
	var/apple/A = new
	ASSERT(A.color() == "red")
	ASSERT(A.taste() == "good")
	ASSERT(A.smell() == "cider")
